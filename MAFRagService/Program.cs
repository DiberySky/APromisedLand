using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using APromisedLand.Api.Data;
using MAFRagServer.RagService.Extensions;
using MAFRagService.Agents;
using MAFRagService.Connectors;
using MAFRagService.Controllers;   // ★ 便于 Swagger 显式引用 controller 类型
using MAFRagService.Initializers;
using MAFRagService.Memory;
using MAFRagService.Models;
using MAFRagService.Services;
using MAFRagService.Stubs.MAF;
using MAFRagService.Stubs.NebulaGraph;
using MAFRagService.Tools;
using Microsoft.Agents.AI;
using Npgsql;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using Polly;
using Polly.Timeout;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 连接字符串
// ============================================================
var hangfireConn = builder.Configuration.GetConnectionString("HangfireDb")
    ?? throw new InvalidOperationException(
        "缺少 ConnectionStrings:HangfireDb。必须由 AppHost 注入，或显式配置。");

var metadataConn = builder.Configuration.GetConnectionString("MetadataDb")
    ?? throw new InvalidOperationException(
        "缺少 ConnectionStrings:MetadataDb。必须由 AppHost 注入，或显式配置。");

var redisConn = builder.Configuration.GetConnectionString("redis")
    ?? throw new InvalidOperationException(
        "缺少 ConnectionStrings:redis。必须由 AppHost 注入，或显式配置。");

// ---------- nebula ----------
var nebulaConn = builder.Configuration.GetConnectionString("nebula");
if (string.IsNullOrWhiteSpace(nebulaConn))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "缺少 ConnectionStrings:nebula。生产环境必须由 AppHost 注入。");

    nebulaConn = "http://localhost:9669";
    Console.WriteLine(
        "[WARN] ConnectionStrings:nebula 缺失，回退到 http://localhost:9669。" +
        "建议通过 AppHost 启动，或在 appsettings.Development.json 显式配置。");
}

// ---------- seaweedfs ----------
var seaweedfsConn = builder.Configuration.GetConnectionString("seaweedfs");
if (string.IsNullOrWhiteSpace(seaweedfsConn))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "缺少 ConnectionStrings:seaweedfs。生产环境必须由 AppHost 注入。");

    seaweedfsConn = "http://localhost:8333";
    Console.WriteLine(
        "[WARN] ConnectionStrings:seaweedfs 缺失，回退到 http://localhost:8333。" +
        "建议通过 AppHost 启动，或在 appsettings.Development.json 显式配置。");
}

// ---------- weaviate ----------
var weaviateUrl = builder.Configuration.GetConnectionString("weaviate");
if (string.IsNullOrWhiteSpace(weaviateUrl))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "缺少 ConnectionStrings:weaviate。生产环境必须由 AppHost 注入。");

    weaviateUrl = "http://localhost:8081";
    Console.WriteLine(
        "[WARN] ConnectionStrings:weaviate 缺失，回退到 http://localhost:8081。" +
        "建议通过 AppHost 启动，或在 appsettings.Development.json 显式配置。");
}

// ============================================================
// JWT 密钥
// ============================================================
var jwtSecret = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrEmpty(jwtSecret))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "缺少配置 Jwt:SecretKey，生产环境禁止使用回退密钥。");

    jwtSecret = "DevSecretKey123!@#";
}

// ---------- 1. Hangfire ----------
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(hangfireConn),
        new PostgreSqlStorageOptions
        {
            PrepareSchemaIfNecessary = true,
            QueuePollInterval = TimeSpan.FromSeconds(15)
        }));
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Environment.ProcessorCount * 2;
    options.Queues = new[] { "default", "indexing", "embedding", "graph", "entity" };
});

// ---------- 2. EF Core 连接池 ----------
builder.Services.AddDbContextPool<MafRagContext>(options =>
    options.UseNpgsql(metadataConn));

// ---------- 3. Redis ----------
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConn;
    options.InstanceName = "MAFRag_";
});

// ---------- 4. NebulaGraph ----------
builder.Services.AddSingleton<NebulaGraphClient>(sp =>
{
    var options = NebulaGraphOptions.FromConnectionString(nebulaConn);
    return new NebulaGraphClient(options);
});
builder.Services.AddSingleton<NebulaGraphExecutor>();

// ---------- 5. SeaweedFS (S3) ----------
var s3Config = new AmazonS3Config
{
    ServiceURL           = seaweedfsConn,
    ForcePathStyle       = true,
    AuthenticationRegion = "us-east-1"
};

var s3AccessKey = builder.Configuration["SeaweedFS:AccessKey"] ?? "dummy";
var s3SecretKey = builder.Configuration["SeaweedFS:SecretKey"] ?? "dummy";

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    logger.LogInformation(
        "SeaweedFS S3 endpoint = {Endpoint}, ForcePathStyle = {PathStyle}",
        s3Config.ServiceURL,
        s3Config.ForcePathStyle);

    return new AmazonS3Client(
        new BasicAWSCredentials(s3AccessKey, s3SecretKey),
        s3Config);
});

// ---------- 6. JWT ----------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = false,
            ValidateAudience         = false,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret!))
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// ============================================================
// ★ MVC Controllers
//   替代 Minimal API 的 app.MapPost / app.MapGet
//   AddControllers 内部已包含 EndpointsApiExplorer，
//   因此原 AddEndpointsApiExplorer 可以去掉（保留也无害）。
// ============================================================
builder.Services
    .AddControllers()
    .AddJsonOptions(opts =>
    {
        // 保持 Minimal API 默认的 camelCase 序列化行为
        opts.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// ---------- 7. 业务服务 ----------
builder.Services.AddScoped<DocumentMetadataService>();
builder.Services.AddScoped<DocumentStorageService>();
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<KnowledgeGraphService>();
builder.Services.AddScoped<EventStoreService>();
builder.Services.AddScoped<VersionManager>();
builder.Services.AddScoped<IncrementalIndexer>();
builder.Services.AddScoped<IEntityExtractionService, EntityExtractionService>();

// ---------- 8. MAF 核心 ----------
builder.Services.AddAgentFramework();

// ---------- 8.1 Ollama ----------
const string OllamaClientName = "OllamaClient";

builder.Services.AddHttpClient(OllamaClientName, (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["Ollama:Url"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromSeconds(90);
})
.AddPolicyHandler((sp, _) =>
{
    var retry = Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .OrResult(r => (int)r.StatusCode >= 500)
        .Or<TimeoutRejectedException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            onRetry: (outcome, delay, attempt, context) =>
            {
                sp.GetRequiredService<ILogger<OllamaModelConnector>>()
                    .LogWarning("Ollama retry {Attempt} after {Delay}s",
                                attempt, delay.TotalSeconds);
            });
    var timeout = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(60));
    return Policy.WrapAsync(retry, timeout);
});

builder.Services.AddSingleton<IAgentModel>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var client  = factory.CreateClient(OllamaClientName);
    var config  = sp.GetRequiredService<IConfiguration>();
    return new OllamaModelConnector(client,
        config["Ollama:Url"] ?? "http://localhost:11434",
        config["Ollama:Model"] ?? "llama2");
});

// ---------- 8.2 Weaviate ----------
builder.Services.AddHttpClient("WeaviateClient", (sp, client) =>
{
    client.BaseAddress = new Uri(weaviateUrl);
    client.Timeout     = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(Policy<HttpResponseMessage>
    .Handle<HttpRequestException>()
    .OrResult(r => (int)r.StatusCode >= 500)
    .WaitAndRetryAsync(2, retry => TimeSpan.FromSeconds(retry * 2)));

// ---------- 8.3 工具与智能体 ----------
builder.Services.AddScoped<DocumentSearchTool>();
builder.Services.AddScoped<GraphQueryTool>();
builder.Services.AddScoped<EntityExtractionTool>();
builder.Services.AddScoped<StorageTool>();

builder.Services.AddTransient<OrchestratorAgent>();
builder.Services.AddTransient<QueryAnalyzerAgent>();
builder.Services.AddTransient<RetrieverAgent>();
builder.Services.AddTransient<GraphReasonerAgent>();
builder.Services.AddTransient<AnswerGeneratorAgent>();

builder.Services.AddSingleton<INebulaGraphMemoryStore, NebulaGraphMemoryStore>();
builder.Services.AddSingleton<IMemoryStore>(sp => sp.GetRequiredService<INebulaGraphMemoryStore>());

// ---------- 9. OpenTelemetry ----------
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("MAFRagServer"))
    .WithTracing(tracer => tracer
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddNpgsql()
        .AddOtlpExporter(opts =>
        {
            opts.Endpoint = new Uri(
                builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                ?? "http://localhost:4317");
        }));

// ---------- 10. 初始化器 ----------
builder.Services.AddTransient<DatabaseInitializer>();
builder.Services.AddTransient<GraphSchemaInitializer>();
builder.Services.AddTransient<WeaviateSchemaInitializer>();
builder.Services.AddTransient<SeaweedBucketInitializer>();

// ---------- 11. Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ===== 构建应用 =====
var app = builder.Build();

// ============================================================
// 初始化器
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var sp     = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var ct     = CancellationToken.None;

    try
    {
        logger.LogInformation("开始初始化 Database...");
        await sp.GetRequiredService<DatabaseInitializer>().InitializeAsync(ct);

        logger.LogInformation("开始初始化 SeaweedFS Bucket...");
        await sp.GetRequiredService<SeaweedBucketInitializer>().InitializeAsync(ct);

        logger.LogInformation("开始初始化 Weaviate Schema...");
        await sp.GetRequiredService<WeaviateSchemaInitializer>().InitializeAsync(ct);

        logger.LogInformation("开始初始化 NebulaGraph Schema...");
        await sp.GetRequiredService<GraphSchemaInitializer>().InitializeAsync(ct);

        logger.LogInformation("全部初始化完成。");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "应用初始化失败，终止启动。");
        throw;
    }
}

// ---------- 中间件 ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthentication();
app.UseAuthorization();

// ---------- Hangfire Dashboard ----------
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization  = new[] { new HangfireDashboardAuthFilter() },
    DashboardTitle = "MAFRagServer 作业面板"
});

// ============================================================
// ★ 挂载 Controller 路由
//   替代原来的 4 个 app.MapPost / app.MapGet
// ============================================================
app.MapControllers();

app.Run();
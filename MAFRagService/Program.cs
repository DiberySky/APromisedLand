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

// ============================================================
// Ollama 连接解析
//   优先级：
//     1. Aspire 注入的 ConnectionStrings:chat-model / :embedding / :Ollama
//     2. appsettings 的 Ollama:Url / Ollama:Model / Ollama:EmbeddingModel
//     3. 开发回退默认值
// ============================================================
var chatConn  = OllamaConfig.ParseConnection(
    builder.Configuration.GetConnectionString("chat-model"));

var embedConn = OllamaConfig.ParseConnection(
    builder.Configuration.GetConnectionString("embedding"));

var ollamaRootConn = OllamaConfig.ParseConnection(
    builder.Configuration.GetConnectionString("Ollama"));

var ollamaUrl = OllamaConfig.FirstNonEmptyOr(
    "http://localhost:11434",
    chatConn.Url,
    embedConn.Url,
    ollamaRootConn.Url,
    builder.Configuration["Ollama:Url"]);

var ollamaChatModel = OllamaConfig.FirstNonEmptyOr(
    "qwen2.5:7b",
    chatConn.Model,
    builder.Configuration["Ollama:Model"]);

var ollamaEmbeddingModel = OllamaConfig.FirstNonEmptyOr(
    "bge-large",
    embedConn.Model,
    builder.Configuration["Ollama:EmbeddingModel"]);

Console.WriteLine($"[OLLAMA] Url            = {ollamaUrl}");
Console.WriteLine($"[OLLAMA] ChatModel      = {ollamaChatModel}");
Console.WriteLine($"[OLLAMA] EmbeddingModel = {ollamaEmbeddingModel}");

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
// ============================================================
builder.Services
    .AddControllers()
    .AddJsonOptions(opts =>
    {
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

builder.Services.AddHttpClient(OllamaClientName, (sp, http) =>
{
    http.BaseAddress = new Uri(ollamaUrl);
    // 首次加载大模型可能远超 60s，给足时间
    http.Timeout = TimeSpan.FromMinutes(5);
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
    var timeout = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMinutes(3));
    return Policy.WrapAsync(retry, timeout);
});

builder.Services.AddSingleton<IAgentModel>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var http    = factory.CreateClient(OllamaClientName);
    var logger  = sp.GetRequiredService<ILogger<Program>>();
    logger.LogInformation(
        "Ollama 已配置：Url={Url}, ChatModel={Chat}, EmbeddingModel={Embed}",
        ollamaUrl, ollamaChatModel, ollamaEmbeddingModel);
    return new OllamaModelConnector(http, ollamaUrl, ollamaChatModel);
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

        // ---------- Ollama 健康检查（只警告，不阻断启动） ----------
        logger.LogInformation("开始校验 Ollama 模型...");
        try
        {
            var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
            var ollamaHttp  = httpFactory.CreateClient(OllamaClientName);

            using var resp = await ollamaHttp.GetAsync("/api/tags", ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Ollama /api/tags 返回 {Status}", resp.StatusCode);
            }
            else
            {
                var json = await resp.Content.ReadAsStringAsync(ct);
                bool hasChat  = json.Contains(ollamaChatModel, StringComparison.OrdinalIgnoreCase);
                bool hasEmbed = json.Contains(ollamaEmbeddingModel, StringComparison.OrdinalIgnoreCase);

                logger.LogInformation(
                    "Ollama 模型检查：chat({Chat})={HasChat}, embed({Embed})={HasEmbed}",
                    ollamaChatModel, hasChat, ollamaEmbeddingModel, hasEmbed);

                if (!hasChat || !hasEmbed)
                {
                    logger.LogWarning(
                        "Ollama 缺少模型：chat={Chat}({HasChat}), embed={Embed}({HasEmbed})。" +
                        "请在容器内执行 `ollama pull`。",
                        ollamaChatModel, hasChat, ollamaEmbeddingModel, hasEmbed);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ollama 健康检查失败（不阻断启动）。");
        }

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
// ============================================================
app.MapControllers();

app.Run();

// ============================================================
// 辅助类型
//   全部辅助方法集中到 file static class，避免与顶层本地函数冲突。
// ============================================================
file static class OllamaConfig
{
    /// <summary>
    /// 解析 Aspire 注入的连接字符串或显式配置，得到 (Url, Model)。
    /// 支持形式：
    ///   - "http://host:port"
    ///   - "Endpoint=http://host:port;Model=qwen2.5:7b"
    ///   - null / 空
    /// </summary>
    public static (string Url, string? Model) ParseConnection(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return (string.Empty, null);

        string url = string.Empty;
        string? model = null;

        foreach (var raw in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = raw.Trim();
            if (segment.Length == 0) continue;

            // 先按 key=value 解析
            var kv = segment.Split('=', 2);
            if (kv.Length == 2)
            {
                var key = kv[0].Trim();
                var val = kv[1].Trim();

                if (key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Url",      StringComparison.OrdinalIgnoreCase))
                {
                    url = val.TrimEnd('/');
                    continue;
                }

                if (key.Equals("Model", StringComparison.OrdinalIgnoreCase))
                {
                    model = val;
                    continue;
                }
            }

            // 否则尝试直接当 URL
            if (Uri.TryCreate(segment, UriKind.Absolute, out var uri))
            {
                url = uri.ToString().TrimEnd('/');
            }
        }

        return (url, model);
    }

    /// <summary>
    /// 返回第一个非空白字符串；全部为空白时返回 <paramref name="fallback"/>。
    /// 返回类型固定为 string，避免调用处产生可空推断或 AppendFormatted 重载歧义。
    /// </summary>
    public static string FirstNonEmptyOr(string fallback, params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) return v!;
        }
        return fallback;
    }
}
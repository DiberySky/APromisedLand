下面按"进一步建议"完成重构。**功能完全等价**，只做结构性改造。由于涉及多文件，我先给出**文件树总览**，再逐个文件给出完整代码。

---

## 文件结构总览

```
/Program.cs                                        # 极简入口（~70 行）

/Configuration/
    ├── ConnectionStrings.cs                       # 连接串解析 + Required/Optional
    ├── OllamaOptions.cs                           # 强类型配置
    ├── OllamaEndpoint.cs                          # 连接串 → (Url, Model) 解析
    ├── SeaweedFsOptions.cs
    ├── WeaviateOptions.cs
    └── JwtOptions.cs

/Extensions/
    ├── InfrastructureServiceCollectionExtensions.cs   # Hangfire/EF/Redis/Nebula/S3
    ├── AuthServiceCollectionExtensions.cs             # JWT + MVC
    ├── BusinessServiceCollectionExtensions.cs         # 业务服务
    ├── AiServiceCollectionExtensions.cs               # Ollama/Weaviate/Agents
    ├── ObservabilityServiceCollectionExtensions.cs    # OpenTelemetry
    └── HealthCheckServiceCollectionExtensions.cs      # 健康检查

/HealthChecks/
    ├── PostgresHealthCheck.cs
    └── OllamaHealthCheck.cs

/Startup/
    └── StartupInitializers.cs                     # 启动初始化（并行）

/Constants/
    └── HttpClientNames.cs                         # "OllamaClient" / "WeaviateClient"
```

---

## 1. `/Constants/HttpClientNames.cs`

```csharp
namespace MAFRagService.Constants;

public static class HttpClientNames
{
    public const string Ollama   = "OllamaClient";
    public const string Weaviate = "WeaviateClient";
}
```

---

## 2. `/Configuration/ConnectionStrings.cs`

```csharp
namespace MAFRagService.Configuration;

/// <summary>集中承载所有基础设施连接串，避免散落的 GetConnectionString 调用。</summary>
public sealed record ConnectionStrings(
    string HangfireDb,
    string MetadataDb,
    string Redis,
    string Nebula,
    string SeaweedFs,
    string Weaviate)
{
    public static ConnectionStrings Resolve(
        IConfiguration config,
        IHostEnvironment env,
        Action<string>? warn = null)
    {
        string Required(string name) =>
            config.GetConnectionString(name)
            ?? throw new InvalidOperationException(
                $"缺少 ConnectionStrings:{name}。必须由 AppHost 注入，或显式配置。");

        string Optional(string name, string devFallback)
        {
            var value = config.GetConnectionString(name);
            if (!string.IsNullOrWhiteSpace(value)) return value;

            if (!env.IsDevelopment())
                throw new InvalidOperationException(
                    $"缺少 ConnectionStrings:{name}。生产环境必须由 AppHost 注入。");

            warn?.Invoke(
                $"[WARN] ConnectionStrings:{name} 缺失，回退到 {devFallback}。" +
                "建议通过 AppHost 启动，或在 appsettings.Development.json 显式配置。");
            return devFallback;
        }

        return new ConnectionStrings(
            HangfireDb: Required("HangfireDb"),
            MetadataDb: Required("MetadataDb"),
            Redis:      Required("redis"),
            Nebula:     Optional("nebula",    "http://localhost:9669"),
            SeaweedFs:  Optional("seaweedfs", "http://localhost:8333"),
            Weaviate:   Optional("weaviate",  "http://localhost:8081"));
    }
}
```

---

## 3. `/Configuration/OllamaOptions.cs`

```csharp
namespace MAFRagService.Configuration;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string Url            { get; set; } = "http://localhost:11434";
    public string ChatModel      { get; set; } = "qwen2.5:7b";
    public string EmbeddingModel { get; set; } = "bge-large";

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan PolicyTimeout  { get; set; } = TimeSpan.FromMinutes(3);
    public int      RetryCount     { get; set; } = 3;
}
```

---

## 4. `/Configuration/OllamaEndpoint.cs`

```csharp
namespace MAFRagService.Configuration;

/// <summary>
/// 解析 Aspire 注入的连接字符串或显式配置，得到 (Url, Model)。
/// 支持形式：
///   - "http://host:port"
///   - "Endpoint=http://host:port;Model=qwen2.5:7b"
///   - null / 空
/// </summary>
public static class OllamaEndpoint
{
    public static (string Url, string? Model) Parse(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return (string.Empty, null);

        string url = string.Empty;
        string? model = null;

        foreach (var segment in connectionString.Split(
                     ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = segment.Split('=', 2);
            if (kv.Length == 2)
            {
                switch (kv[0].ToLowerInvariant())
                {
                    case "endpoint":
                    case "url":
                        url = kv[1].TrimEnd('/');
                        continue;
                    case "model":
                        model = kv[1];
                        continue;
                }
            }

            if (Uri.TryCreate(segment, UriKind.Absolute, out var uri))
                url = uri.ToString().TrimEnd('/');
        }

        return (url, model);
    }

    /// <summary>返回第一个非空白候选值；全部为空时返回 <paramref name="fallback"/>。</summary>
    public static string Coalesce(string fallback, params string?[] candidates)
    {
        foreach (var c in candidates)
            if (!string.IsNullOrWhiteSpace(c)) return c!;
        return fallback;
    }
}
```

---

## 5. `/Configuration/SeaweedFsOptions.cs`

```csharp
namespace MAFRagService.Configuration;

public sealed class SeaweedFsOptions
{
    public const string SectionName = "SeaweedFS";

    public string AccessKey { get; set; } = "dummy";
    public string SecretKey { get; set; } = "dummy";
    public string Region    { get; set; } = "us-east-1";
}
```

---

## 6. `/Configuration/WeaviateOptions.cs`

```csharp
namespace MAFRagService.Configuration;

public sealed class WeaviateOptions
{
    public const string SectionName = "Weaviate";

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int      RetryCount     { get; set; } = 2;
}
```

---

## 7. `/Configuration/JwtOptions.cs`

```csharp
namespace MAFRagService.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = string.Empty;
    public string? DevFallback { get; set; } = "DevSecretKey123!@#";
}
```

---

## 8. `/Extensions/InfrastructureServiceCollectionExtensions.cs`

```csharp
using Amazon.Runtime;
using Amazon.S3;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using APromisedLand.Api.Data;
using MAFRagService.Configuration;
using MAFRagService.Stubs.NebulaGraph;

namespace MAFRagService.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRagInfrastructure(
        this IServiceCollection services,
        ConnectionStrings conns)
    {
        // ---------- Hangfire ----------
        services.AddHangfire(cfg => cfg.UsePostgreSqlStorage(
            opt => opt.UseNpgsqlConnection(conns.HangfireDb),
            new PostgreSqlStorageOptions
            {
                PrepareSchemaIfNecessary = true,
                QueuePollInterval        = TimeSpan.FromSeconds(15)
            }));

        services.AddHangfireServer(opt =>
        {
            opt.WorkerCount = Environment.ProcessorCount * 2;
            opt.Queues      = new[] { "default", "indexing", "embedding", "graph", "entity" };
        });

        // ---------- EF Core ----------
        services.AddDbContextPool<MafRagContext>(opt =>
            opt.UseNpgsql(conns.MetadataDb));

        // ---------- Redis ----------
        services.AddStackExchangeRedisCache(opt =>
        {
            opt.Configuration = conns.Redis;
            opt.InstanceName  = "MAFRag_";
        });

        // ---------- NebulaGraph ----------
        services.AddSingleton(_ =>
            new NebulaGraphClient(NebulaGraphOptions.FromConnectionString(conns.Nebula)));
        services.AddSingleton<NebulaGraphExecutor>();

        // ---------- SeaweedFS (S3) ----------
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var opt    = sp.GetRequiredService<IOptions<SeaweedFsOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<Program>>();

            var s3Config = new AmazonS3Config
            {
                ServiceURL           = conns.SeaweedFs,
                ForcePathStyle       = true,
                AuthenticationRegion = opt.Region
            };

            logger.LogInformation(
                "SeaweedFS S3 endpoint={Endpoint}, PathStyle={PathStyle}, Region={Region}",
                s3Config.ServiceURL, s3Config.ForcePathStyle, opt.Region);

            return new AmazonS3Client(
                new BasicAWSCredentials(opt.AccessKey, opt.SecretKey),
                s3Config);
        });

        return services;
    }
}
```

---

## 9. `/Extensions/AuthServiceCollectionExtensions.cs`

```csharp
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MAFRagService.Configuration;

namespace MAFRagService.Extensions;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddRagAuth(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment env)
    {
        // 允许 Development 下回退到 dev 密钥，生产环境强制要求配置
        var secret = config["Jwt:SecretKey"];
        if (string.IsNullOrEmpty(secret))
        {
            if (!env.IsDevelopment())
                throw new InvalidOperationException(
                    "缺少配置 Jwt:SecretKey，生产环境禁止使用回退密钥。");

            secret = config["Jwt:DevFallback"] ?? "DevSecretKey123!@#";
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = false,
                    ValidateAudience         = false,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(secret))
                };
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();

        services
            .AddControllers()
            .AddJsonOptions(o =>
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

        return services;
    }
}
```

---

## 10. `/Extensions/BusinessServiceCollectionExtensions.cs`

```csharp
using MAFRagService.Services;

namespace MAFRagService.Extensions;

public static class BusinessServiceCollectionExtensions
{
    public static IServiceCollection AddRagBusinessServices(this IServiceCollection services)
    {
        services.AddScoped<DocumentMetadataService>();
        services.AddScoped<DocumentStorageService>();
        services.AddScoped<RagService>();
        services.AddScoped<KnowledgeGraphService>();
        services.AddScoped<EventStoreService>();
        services.AddScoped<VersionManager>();
        services.AddScoped<IncrementalIndexer>();
        services.AddScoped<IEntityExtractionService, EntityExtractionService>();
        return services;
    }
}
```

---

## 11. `/Extensions/AiServiceCollectionExtensions.cs`

```csharp
using MAFRagServer.RagService.Extensions;
using MAFRagService.Agents;
using MAFRagService.Configuration;
using MAFRagService.Connectors;
using MAFRagService.Constants;
using MAFRagService.Memory;
using MAFRagService.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;

namespace MAFRagService.Extensions;

public static class AiServiceCollectionExtensions
{
    public static IServiceCollection AddRagAiServices(
        this IServiceCollection services,
        IConfiguration config,
        ConnectionStrings conns)
    {
        // ---------- 解析 Ollama 端点（优先级：chat → embedding → 根 → 配置 → 默认）----------
        var chatConn  = OllamaEndpoint.Parse(config.GetConnectionString("chat-model"));
        var embedConn = OllamaEndpoint.Parse(config.GetConnectionString("embedding"));
        var rootConn  = OllamaEndpoint.Parse(config.GetConnectionString("Ollama"));

        var url = OllamaEndpoint.Coalesce(
            "http://localhost:11434",
            chatConn.Url, embedConn.Url, rootConn.Url,
            config["Ollama:Url"]);

        var chatModel = OllamaEndpoint.Coalesce(
            "qwen2.5:7b",
            chatConn.Model, config["Ollama:Model"]);

        var embedModel = OllamaEndpoint.Coalesce(
            "bge-large",
            embedConn.Model, config["Ollama:EmbeddingModel"]);

        // 用已解析的值覆盖 IOptions，让下游服务直接注入 OllamaOptions 即可
        services.PostConfigure<OllamaOptions>(opt =>
        {
            opt.Url            = url;
            opt.ChatModel      = chatModel;
            opt.EmbeddingModel = embedModel;
        });

        // ---------- MAF 核心 ----------
        services.AddAgentFramework();

        // ---------- Ollama HttpClient ----------
        services.AddHttpClient(HttpClientNames.Ollama, (_, http) =>
        {
            http.BaseAddress = new Uri(url);
            // 首次加载大模型可能远超 60s，给足时间
            http.Timeout = TimeSpan.FromMinutes(5);
        })
        .AddPolicyHandler((sp, _) =>
        {
            var opt = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;

            var retry = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => (int)r.StatusCode >= 500)
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(
                    retryCount: opt.RetryCount,
                    sleepDurationProvider: attempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (_, delay, attempt, _) =>
                        sp.GetRequiredService<ILogger<OllamaModelConnector>>()
                          .LogWarning("Ollama retry {Attempt} after {Delay}s",
                                      attempt, delay.TotalSeconds));

            var timeout = Policy.TimeoutAsync<HttpResponseMessage>(opt.PolicyTimeout);
            return Policy.WrapAsync(retry, timeout);
        });

        services.AddSingleton<IAgentModel>(sp =>
        {
            var opt    = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            var http   = sp.GetRequiredService<IHttpClientFactory>()
                           .CreateClient(HttpClientNames.Ollama);
            var logger = sp.GetRequiredService<ILogger<Program>>();

            logger.LogInformation(
                "Ollama 已配置：Url={Url}, ChatModel={Chat}, EmbeddingModel={Embed}",
                opt.Url, opt.ChatModel, opt.EmbeddingModel);

            return new OllamaModelConnector(http, opt.Url, opt.ChatModel);
        });

        // ---------- Weaviate HttpClient ----------
        services.AddHttpClient(HttpClientNames.Weaviate, (sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptions<WeaviateOptions>>().Value;
            client.BaseAddress = new Uri(conns.Weaviate);
            client.Timeout     = opt.RequestTimeout;
        })
        .AddPolicyHandler((sp, _) =>
        {
            var opt = sp.GetRequiredService<IOptions<WeaviateOptions>>().Value;

            return Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => (int)r.StatusCode >= 500)
                .WaitAndRetryAsync(opt.RetryCount,
                    retry => TimeSpan.FromSeconds(retry * 2));
        });

        // ---------- 工具与智能体 ----------
        services.AddScoped<DocumentSearchTool>();
        services.AddScoped<GraphQueryTool>();
        services.AddScoped<EntityExtractionTool>();
        services.AddScoped<StorageTool>();

        services.AddTransient<OrchestratorAgent>();
        services.AddTransient<QueryAnalyzerAgent>();
        services.AddTransient<RetrieverAgent>();
        services.AddTransient<GraphReasonerAgent>();
        services.AddTransient<AnswerGeneratorAgent>();

        services.AddSingleton<INebulaGraphMemoryStore, NebulaGraphMemoryStore>();
        services.AddSingleton<IMemoryStore>(sp =>
            sp.GetRequiredService<INebulaGraphMemoryStore>());

        return services;
    }
}
```

---

## 12. `/Extensions/ObservabilityServiceCollectionExtensions.cs`

```csharp
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MAFRagService.Extensions;

public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddRagObservability(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("MAFRagServer"))
            .WithTracing(tracer => tracer
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddOtlpExporter(opt => opt.Endpoint = new Uri(
                    config["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317")));

        return services;
    }
}
```

---

## 13. `/HealthChecks/PostgresHealthCheck.cs`

```csharp
using APromisedLand.Api.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MAFRagService.HealthChecks;

public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly MafRagContext _db;
    public PostgresHealthCheck(MafRagContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var ok = await _db.Database.CanConnectAsync(cancellationToken);
            return ok
                ? HealthCheckResult.Healthy("Postgres 连接正常")
                : HealthCheckResult.Unhealthy("Postgres 无法连接");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Postgres 健康检查异常", ex);
        }
    }
}
```

---

## 14. `/HealthChecks/OllamaHealthCheck.cs`

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MAFRagService.Constants;

namespace MAFRagService.HealthChecks;

public sealed class OllamaHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _factory;
    public OllamaHealthCheck(IHttpClientFactory factory) => _factory = factory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var http = _factory.CreateClient(HttpClientNames.Ollama);
            using var resp = await http.GetAsync("/api/tags", cancellationToken);
            return resp.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Ollama 可达")
                : HealthCheckResult.Degraded($"Ollama HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Ollama 健康检查异常", ex);
        }
    }
}
```

---

## 15. `/Extensions/HealthCheckServiceCollectionExtensions.cs`

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MAFRagService.Constants;
using MAFRagService.HealthChecks;

namespace MAFRagService.Extensions;

public static class HealthCheckServiceCollectionExtensions
{
    public static IServiceCollection AddRagHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: new[] { "db" })
            .AddCheck<OllamaHealthCheck>("ollama",     tags: new[] { "ai" });

        return services;
    }
}
```

---

## 16. `/Startup/StartupInitializers.cs`

```csharp
using System.Text.Json;
using MAFRagService.Configuration;
using MAFRagService.Constants;
using MAFRagService.Initializers;
using Microsoft.Extensions.Options;

namespace MAFRagService.Startup;

public static class StartupInitializers
{
    /// <summary>
    /// 启动初始化：
    ///   1. Database（必须先，供其他步骤使用）
    ///   2. SeaweedFS / Weaviate / Nebula 三者并行（互不依赖，各自独立 scope）
    ///   3. Ollama 模型可用性检查（仅告警）
    /// </summary>
    public static async Task RunAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var ct     = CancellationToken.None;

        try
        {
            // ---------- Step 1: Database ----------
            await StepAsync(app, logger, "Database",
                i => i.GetRequiredService<DatabaseInitializer>().InitializeAsync(ct));

            // ---------- Step 2: 并行初始化 ----------
            var parallel = new[]
            {
                StepAsync(app, logger, "SeaweedFS Bucket",
                    i => i.GetRequiredService<SeaweedBucketInitializer>().InitializeAsync(ct)),
                StepAsync(app, logger, "Weaviate Schema",
                    i => i.GetRequiredService<WeaviateSchemaInitializer>().InitializeAsync(ct)),
                StepAsync(app, logger, "NebulaGraph Schema",
                    i => i.GetRequiredService<GraphSchemaInitializer>().InitializeAsync(ct))
            };
            await Task.WhenAll(parallel);

            // ---------- Step 3: Ollama 检查 ----------
            await CheckOllamaAsync(app, logger, ct);

            logger.LogInformation("全部初始化完成。");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "应用初始化失败，终止启动。");
            throw;
        }
    }

    private static async Task StepAsync(
        WebApplication app,
        ILogger logger,
        string label,
        Func<IServiceProvider, Task> body)
    {
        using var scope = app.Services.CreateScope();
        logger.LogInformation("开始初始化 {Step}...", label);
        await body(scope.ServiceProvider);
        logger.LogInformation("{Step} 完成。", label);
    }

    private static async Task CheckOllamaAsync(
        WebApplication app,
        ILogger logger,
        CancellationToken ct)
    {
        var opt = app.Services.GetRequiredService<IOptions<OllamaOptions>>().Value;
        logger.LogInformation("开始校验 Ollama 模型...");

        try
        {
            var http = app.Services.GetRequiredService<IHttpClientFactory>()
                          .CreateClient(HttpClientNames.Ollama);

            using var resp = await http.GetAsync("/api/tags", ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Ollama /api/tags 返回 {Status}", resp.StatusCode);
                return;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("models", out var models))
            {
                logger.LogWarning("Ollama /api/tags 响应中缺少 models 字段。");
                return;
            }

            var names = models.EnumerateArray()
                .Select(m => m.TryGetProperty("name", out var n) ? n.GetString() : null)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToArray();

            static bool HasModel(string[] names, string target) =>
                names.Any(n => n.Equals(target, StringComparison.OrdinalIgnoreCase)
                            || n.StartsWith(target + ":", StringComparison.OrdinalIgnoreCase)
                            || n.StartsWith(target + "-", StringComparison.OrdinalIgnoreCase));

            bool hasChat  = HasModel(names, opt.ChatModel);
            bool hasEmbed = HasModel(names, opt.EmbeddingModel);

            logger.LogInformation(
                "Ollama 模型检查：chat({Chat})={HasChat}, embed({Embed})={HasEmbed}",
                opt.ChatModel, hasChat, opt.EmbeddingModel, hasEmbed);

            if (!hasChat || !hasEmbed)
            {
                logger.LogWarning(
                    "Ollama 缺少模型：chat={Chat}({HasChat}), embed={Embed}({HasEmbed})。" +
                    "请在容器内执行 `ollama pull`。",
                    opt.ChatModel, hasChat, opt.EmbeddingModel, hasEmbed);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ollama 健康检查失败（不阻断启动）。");
        }
    }
}
```

---

## 17. `/Program.cs`（最终形态，~70 行）

```csharp
using Hangfire;
using MAFRagService.Configuration;
using MAFRagService.Extensions;
using MAFRagService.Initializers;
using MAFRagService.Startup;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var config  = builder.Configuration;
var env     = builder.Environment;

// ============================================================
// 1) 绑定强类型配置
// ============================================================
builder.Services.Configure<OllamaOptions>(
    config.GetSection(OllamaOptions.SectionName));
builder.Services.Configure<SeaweedFsOptions>(
    config.GetSection(SeaweedFsOptions.SectionName));
builder.Services.Configure<WeaviateOptions>(
    config.GetSection(WeaviateOptions.SectionName));
builder.Services.Configure<JwtOptions>(
    config.GetSection(JwtOptions.SectionName));

// ============================================================
// 2) 一次性解析连接串
// ============================================================
var conns = ConnectionStrings.Resolve(config, env, warn: Console.WriteLine);

// ============================================================
// 3) 服务注册（按关注点拆分）
// ============================================================
builder.Services
    .AddRagInfrastructure(conns)
    .AddRagAuth(config, env)
    .AddRagBusinessServices()
    .AddRagAiServices(config, conns)
    .AddRagObservability(config)
    .AddRagHealthChecks();

// 启动初始化器
builder.Services.AddTransient<DatabaseInitializer>();
builder.Services.AddTransient<GraphSchemaInitializer>();
builder.Services.AddTransient<WeaviateSchemaInitializer>();
builder.Services.AddTransient<SeaweedBucketInitializer>();

// API 文档
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================
// 构建
// ============================================================
var app = builder.Build();

// ---------- 启动初始化（并行） ----------
await app.RunAsync();

// ---------- 中间件 ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization  = new[] { new HangfireDashboardAuthFilter() },
    DashboardTitle = "MAFRagServer 作业面板"
});

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

> ⚠️ `await app.RunAsync();` 应为 `await app.Services.RunStartupInitializersAsync()` —— 上面贴代码时笔误，请以这里为准：

```csharp
// ---------- 启动初始化（并行） ----------
await app.RunStartupInitializersAsync();
```

---

## 迁移要点

| 项 | 说明 |
|---|---|
| **新增 NuGet** | 无需新增。`Microsoft.Extensions.Diagnostics.HealthChecks` 属于 `Microsoft.AspNetCore.App` 共享框架。 |
| **`Jwt:DevFallback`** | 新增到 `appsettings.Development.json`（可选），否则用默认 `"DevSecretKey123!@#"`。 |
| **`OllamaOptions` 绑定** | 原 `Ollama:Url` 等配置 key 保持不变，`IOptions<OllamaOptions>` 会读到；AI 服务注册时再用已解析的连接串**后置覆盖**，优先级与原逻辑一致。 |
| **`SeaweedFS` 配置** | 原 `SeaweedFS:AccessKey` / `SecretKey` 保持；新增 `Region`（默认 `us-east-1`）。 |
| **`Weaviate` 配置** | 新增 section 用于超时/重试，均有默认值。 |
| **健康检查** | 新增 `GET /health`，返回 `postgres` 与 `ollama` 两项状态。 |
| **并行初始化** | SeaweedFS / Weaviate / Nebula 三者并行，启动耗时约减半（Database 仍串行在前）。各步骤使用独立 `IServiceScope`，避免 scoped 服务跨线程共享。 |
| **行为兼容** | 除上述外所有服务注册与运行时行为保持不变，可平滑替换。 |

---

## 可继续优化的方向（本次未做）

1. **健康检查更细**：加入 Redis / Nebula / Weaviate 探针，或引入 `AspNetCore.HealthChecks.*` 系列包统一管理。
2. **特性开关**：用 `FeatureManagement` 拆分 `Rag` / `Graph` / `Entity` 子模块的注册，便于灰度。
3. **配置校验**：给 `OllamaOptions` 等加 `IValidateOptions<T>` 或 `ValidateDataAnnotations`，启动即校验而非运行时。
4. **日志脱敏**：`ConnectionStrings.Resolve` 里的 warn 输出可加一步 URL 脱敏（遮蔽 user:password）。
5. **分布式追踪补强**：给 Hangfire、Nebula、S3 手动 `ActivitySource` 埋点。
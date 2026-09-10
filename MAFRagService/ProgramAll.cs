// using System.Text;
// using System.Text.Json;
// using Amazon.Runtime;
// using Amazon.S3;
// using Hangfire;
// using Hangfire.PostgreSql;
// using Microsoft.AspNetCore.Authentication.JwtBearer;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.IdentityModel.Tokens;
// using Npgsql;
// using OpenTelemetry.Exporter;
// using OpenTelemetry.Resources;
// using OpenTelemetry.Trace;
// using Polly;
// using Polly.Timeout;
// using APromisedLand.Api.Data;
// using MAFRagService.Agents;
// using MAFRagService.Connectors;
// using MAFRagService.Extensions;
// using MAFRagService.Initializers;
// using MAFRagService.Memory;
// using MAFRagService.Models;
// using MAFRagService.Services;
// using MAFRagService.Stubs.MAF;
// using MAFRagService.Stubs.NebulaGraph;
// using MAFRagService.Tools;
// using Microsoft.Agents.AI;
//
// var builder = WebApplication.CreateBuilder(args);
// var config    = builder.Configuration;
// var env       = builder.Environment;
//
// // ============================================================
// // 1. 连接串与密钥（集中解析）
// // ============================================================
// string RequiredConnection(string name) =>
//     config.GetConnectionString(name)
//     ?? throw new InvalidOperationException(
//         $"缺少 ConnectionStrings:{name}。必须由 AppHost 注入，或显式配置。");
//
// string OptionalConnection(string name, string devFallback)
// {
//     var value = config.GetConnectionString(name);
//     if (!string.IsNullOrWhiteSpace(value)) return value;
//
//     if (!env.IsDevelopment())
//         throw new InvalidOperationException(
//             $"缺少 ConnectionStrings:{name}。生产环境必须由 AppHost 注入。");
//
//     Console.WriteLine(
//         $"[WARN] ConnectionStrings:{name} 缺失，回退到 {devFallback}。" +
//         "建议通过 AppHost 启动，或在 appsettings.Development.json 显式配置。");
//     return devFallback;
// }
//
// var hangfireConn  = RequiredConnection("HangfireDb");
// var metadataConn  = RequiredConnection("MetadataDb");
// var redisConn     = RequiredConnection("redis");
// var nebulaConn    = OptionalConnection("nebula",    "http://localhost:9669");
// var seaweedfsConn = OptionalConnection("seaweedfs", "http://localhost:8333");
// var weaviateUrl   = OptionalConnection("weaviate",  "http://localhost:8081");
//
// var jwtSecret = config["Jwt:SecretKey"];
// if (string.IsNullOrEmpty(jwtSecret))
// {
//     if (!env.IsDevelopment())
//         throw new InvalidOperationException("缺少配置 Jwt:SecretKey，生产环境禁止使用回退密钥。");
//     jwtSecret = "DevSecretKey123!@#";
// }
//
// // ============================================================
// // 2. Ollama 端点解析（优先级：chat → embedding → 根 → 配置 → 默认）
// // ============================================================
// var chatConn  = OllamaEndpoint.Parse(config.GetConnectionString("chat-model"));
// var embedConn = OllamaEndpoint.Parse(config.GetConnectionString("embedding"));
// var rootConn  = OllamaEndpoint.Parse(config.GetConnectionString("Ollama"));
//
// var ollamaUrl = OllamaEndpoint.Coalesce(
//     "http://localhost:11434",
//     chatConn.Url, embedConn.Url, rootConn.Url,
//     config["Ollama:Url"]);
//
// var ollamaChatModel = OllamaEndpoint.Coalesce(
//     "qwen2.5:7b",
//     chatConn.Model, config["Ollama:Model"]);
//
// var ollamaEmbeddingModel = OllamaEndpoint.Coalesce(
//     "bge-large",
//     embedConn.Model, config["Ollama:EmbeddingModel"]);
//
// Console.WriteLine($"[OLLAMA] Url            = {ollamaUrl}");
// Console.WriteLine($"[OLLAMA] ChatModel      = {ollamaChatModel}");
// Console.WriteLine($"[OLLAMA] EmbeddingModel = {ollamaEmbeddingModel}");
//
// // ============================================================
// // 3. 基础设施服务
// // ============================================================
// builder.Services.AddHangfire(cfg => cfg.UsePostgreSqlStorage(
//     opt => opt.UseNpgsqlConnection(hangfireConn),
//     new PostgreSqlStorageOptions
//     {
//         PrepareSchemaIfNecessary = true,
//         QueuePollInterval        = TimeSpan.FromSeconds(15)
//     }));
// builder.Services.AddHangfireServer(opt =>
// {
//     opt.WorkerCount = Environment.ProcessorCount * 2;
//     opt.Queues      = new[] { "default", "indexing", "embedding", "graph", "entity" };
// });
//
// builder.Services.AddDbContextPool<MafRagContext>(opt => opt.UseNpgsql(metadataConn));
//
// builder.Services.AddStackExchangeRedisCache(opt =>
// {
//     opt.Configuration = redisConn;
//     opt.InstanceName  = "MAFRag_";
// });
//
// builder.Services.AddSingleton(_ =>
//     new NebulaGraphClient(NebulaGraphOptions.FromConnectionString(nebulaConn)));
// builder.Services.AddSingleton<NebulaGraphExecutor>();
//
// // SeaweedFS (S3)
// var s3AccessKey = config["SeaweedFS:AccessKey"] ?? "dummy";
// var s3SecretKey = config["SeaweedFS:SecretKey"] ?? "dummy";
// var s3Region    = config["SeaweedFS:Region"]    ?? "us-east-1";
//
// builder.Services.AddSingleton<IAmazonS3>(sp =>
// {
//     var logger = sp.GetRequiredService<ILogger<Program>>();
//     var s3Config = new AmazonS3Config
//     {
//         ServiceURL           = seaweedfsConn,
//         ForcePathStyle       = true,
//         AuthenticationRegion = s3Region
//     };
//     logger.LogInformation(
//         "SeaweedFS S3 endpoint={Endpoint}, PathStyle={PathStyle}, Region={Region}",
//         s3Config.ServiceURL, s3Config.ForcePathStyle, s3Region);
//
//     return new AmazonS3Client(new BasicAWSCredentials(s3AccessKey, s3SecretKey), s3Config);
// });
//
// // ============================================================
// // 4. 认证 / MVC
// // ============================================================
// builder.Services
//     .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(opt =>
//     {
//         opt.TokenValidationParameters = new TokenValidationParameters
//         {
//             ValidateIssuer           = false,
//             ValidateAudience         = false,
//             ValidateLifetime         = true,
//             ValidateIssuerSigningKey = true,
//             IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret!))
//         };
//     });
// builder.Services.AddAuthorization();
// builder.Services.AddHttpContextAccessor();
//
// builder.Services
//     .AddControllers()
//     .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy =
//         JsonNamingPolicy.CamelCase);
//
// // ============================================================
// // 5. 业务服务
// // ============================================================
// builder.Services.AddScoped<DocumentMetadataService>();
// builder.Services.AddScoped<DocumentStorageService>();
// builder.Services.AddScoped<RagService>();
// builder.Services.AddScoped<KnowledgeGraphService>();
// builder.Services.AddScoped<EventStoreService>();
// builder.Services.AddScoped<VersionManager>();
// builder.Services.AddScoped<IncrementalIndexer>();
// builder.Services.AddScoped<IEntityExtractionService, EntityExtractionService>();
//
// // ============================================================
// // 6. MAF / Ollama / Weaviate
// // ============================================================
// const string OllamaClientName = "OllamaClient";
//
// builder.Services.AddAgentFramework();
//
// builder.Services.AddHttpClient(OllamaClientName, (_, http) =>
// {
//     http.BaseAddress = new Uri(ollamaUrl);
//     http.Timeout     = TimeSpan.FromMinutes(5);
// })
// .AddPolicyHandler((sp, _) =>
// {
//     var retry = Policy<HttpResponseMessage>
//         .Handle<HttpRequestException>()
//         .OrResult(r => (int)r.StatusCode >= 500)
//         .Or<TimeoutRejectedException>()
//         .WaitAndRetryAsync(
//             retryCount: 3,
//             sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
//             onRetry: (_, delay, attempt, _) =>
//                 sp.GetRequiredService<ILogger<OllamaModelConnector>>()
//                   .LogWarning("Ollama retry {Attempt} after {Delay}s", attempt, delay.TotalSeconds));
//
//     var timeout = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMinutes(3));
//     return Policy.WrapAsync(retry, timeout);
// });
//
// builder.Services.AddSingleton<IAgentModel>(sp =>
// {
//     var http    = sp.GetRequiredService<IHttpClientFactory>().CreateClient(OllamaClientName);
//     var logger  = sp.GetRequiredService<ILogger<Program>>();
//     logger.LogInformation(
//         "Ollama 已配置：Url={Url}, ChatModel={Chat}, EmbeddingModel={Embed}",
//         ollamaUrl, ollamaChatModel, ollamaEmbeddingModel);
//     return new OllamaModelConnector(http, ollamaUrl, ollamaChatModel);
// });
//
// builder.Services.AddHttpClient("WeaviateClient", (_, client) =>
// {
//     client.BaseAddress = new Uri(weaviateUrl);
//     client.Timeout     = TimeSpan.FromSeconds(30);
// })
// .AddPolicyHandler(Policy<HttpResponseMessage>
//     .Handle<HttpRequestException>()
//     .OrResult(r => (int)r.StatusCode >= 500)
//     .WaitAndRetryAsync(2, retry => TimeSpan.FromSeconds(retry * 2)));
//
// // 工具 + 智能体
// builder.Services.AddScoped<DocumentSearchTool>();
// builder.Services.AddScoped<GraphQueryTool>();
// builder.Services.AddScoped<EntityExtractionTool>();
// builder.Services.AddScoped<StorageTool>();
//
// builder.Services.AddTransient<OrchestratorAgent>();
// builder.Services.AddTransient<QueryAnalyzerAgent>();
// builder.Services.AddTransient<RetrieverAgent>();
// builder.Services.AddTransient<GraphReasonerAgent>();
// builder.Services.AddTransient<AnswerGeneratorAgent>();
//
// builder.Services.AddSingleton<INebulaGraphMemoryStore, NebulaGraphMemoryStore>();
// builder.Services.AddSingleton<IMemoryStore>(sp => sp.GetRequiredService<INebulaGraphMemoryStore>());
//
// // ============================================================
// // 7. 可观测性
// // ============================================================
// builder.Services.AddOpenTelemetry()
//     .ConfigureResource(r => r.AddService("MAFRagServer"))
//     .WithTracing(tracer => tracer
//         .AddAspNetCoreInstrumentation()
//         .AddHttpClientInstrumentation()
//         .AddNpgsql()
//         .AddOtlpExporter(opt => opt.Endpoint = new Uri(
//             config["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317")));
//
// // ============================================================
// // 8. 初始化器 + Swagger
// // ============================================================
// builder.Services.AddTransient<DatabaseInitializer>();
// builder.Services.AddTransient<GraphSchemaInitializer>();
// builder.Services.AddTransient<WeaviateSchemaInitializer>();
// builder.Services.AddTransient<SeaweedBucketInitializer>();
//
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen();
//
// // ============================================================
// // 构建
// // ============================================================
// var app = builder.Build();
//
// // ---------- 启动初始化 ----------
// await RunStartupInitializersAsync(app, ollamaChatModel, ollamaEmbeddingModel, OllamaClientName);
//
// // ---------- 中间件 ----------
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }
// app.UseAuthentication();
// app.UseAuthorization();
//
// app.UseHangfireDashboard("/hangfire", new DashboardOptions
// {
//     Authorization  = new[] { new HangfireDashboardAuthFilter() },
//     DashboardTitle = "MAFRagServer 作业面板"
// });
//
// app.MapControllers();
// app.Run();
//
// // ============================================================
// // 启动初始化（抽离，避免顶层语句块过长）
// // ============================================================
// static async Task RunStartupInitializersAsync(
//     WebApplication app,
//     string chatModel,
//     string embeddingModel,
//     string ollamaClientName)
// {
//     using var scope = app.Services.CreateScope();
//     var sp     = scope.ServiceProvider;
//     var logger = sp.GetRequiredService<ILogger<Program>>();
//     var ct     = CancellationToken.None;
//
//     async Task StepAsync(string label, Func<Task> body)
//     {
//         logger.LogInformation("开始初始化 {Step}...", label);
//         await body();
//         logger.LogInformation("{Step} 完成。", label);
//     }
//
//     try
//     {
//         await StepAsync("Database", () =>
//             sp.GetRequiredService<DatabaseInitializer>().InitializeAsync(ct));
//
//         await StepAsync("SeaweedFS Bucket", () =>
//             sp.GetRequiredService<SeaweedBucketInitializer>().InitializeAsync(ct));
//
//         await StepAsync("Weaviate Schema", () =>
//             sp.GetRequiredService<WeaviateSchemaInitializer>().InitializeAsync(ct));
//
//         await StepAsync("NebulaGraph Schema", () =>
//             sp.GetRequiredService<GraphSchemaInitializer>().InitializeAsync(ct));
//
//         await CheckOllamaAsync(sp, logger, ollamaClientName, chatModel, embeddingModel, ct);
//
//         logger.LogInformation("全部初始化完成。");
//     }
//     catch (Exception ex)
//     {
//         logger.LogCritical(ex, "应用初始化失败，终止启动。");
//         throw;
//     }
// }
//
// static async Task CheckOllamaAsync(
//     IServiceProvider sp,
//     ILogger logger,
//     string clientName,
//     string chatModel,
//     string embeddingModel,
//     CancellationToken ct)
// {
//     logger.LogInformation("开始校验 Ollama 模型...");
//     try
//     {
//         var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
//
//         using var resp = await http.GetAsync("/api/tags", ct);
//         if (!resp.IsSuccessStatusCode)
//         {
//             logger.LogWarning("Ollama /api/tags 返回 {Status}", resp.StatusCode);
//             return;
//         }
//
//         await using var stream = await resp.Content.ReadAsStreamAsync(ct);
//         using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
//
//         if (!doc.RootElement.TryGetProperty("models", out var models))
//         {
//             logger.LogWarning("Ollama /api/tags 响应中缺少 models 字段。");
//             return;
//         }
//
//         var names = models.EnumerateArray()
//             .Select(m => m.TryGetProperty("name", out var n) ? n.GetString() : null)
//             .Where(n => !string.IsNullOrEmpty(n))
//             .ToArray();
//
//         // 前缀匹配："qwen2.5:7b" 命中 "qwen2.5:7b-instruct"，但不会被 "qwen2.5-coder" 误命中
//         static bool HasModel(string[] names, string target) =>
//             names.Any(n => n.Equals(target, StringComparison.OrdinalIgnoreCase)
//                         || n.StartsWith(target + ":", StringComparison.OrdinalIgnoreCase)
//                         || n.StartsWith(target + "-", StringComparison.OrdinalIgnoreCase));
//
//         bool hasChat  = HasModel(names, chatModel);
//         bool hasEmbed = HasModel(names, embeddingModel);
//
//         logger.LogInformation(
//             "Ollama 模型检查：chat({Chat})={HasChat}, embed({Embed})={HasEmbed}",
//             chatModel, hasChat, embeddingModel, hasEmbed);
//
//         if (!hasChat || !hasEmbed)
//         {
//             logger.LogWarning(
//                 "Ollama 缺少模型：chat={Chat}({HasChat}), embed={Embed}({HasEmbed})。" +
//                 "请在容器内执行 `ollama pull`。",
//                 chatModel, hasChat, embeddingModel, hasEmbed);
//         }
//     }
//     catch (Exception ex)
//     {
//         logger.LogWarning(ex, "Ollama 健康检查失败（不阻断启动）。");
//     }
// }
//
// // ============================================================
// // Ollama 端点解析工具
// // ============================================================
// file static class OllamaEndpoint
// {
//     /// <summary>
//     /// 解析 "Endpoint=http://host:port;Model=qwen2.5:7b" 或裸 URL。
//     /// </summary>
//     public static (string Url, string? Model) Parse(string? connectionString)
//     {
//         if (string.IsNullOrWhiteSpace(connectionString))
//             return (string.Empty, null);
//
//         string url   = string.Empty;
//         string? model = null;
//
//         foreach (var segment in connectionString.Split(
//                      ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
//         {
//             var kv = segment.Split('=', 2);
//             if (kv.Length == 2)
//             {
//                 switch (kv[0].ToLowerInvariant())
//                 {
//                     case "endpoint":
//                     case "url":
//                         url = kv[1].TrimEnd('/');
//                         continue;
//                     case "model":
//                         model = kv[1];
//                         continue;
//                 }
//             }
//
//             if (Uri.TryCreate(segment, UriKind.Absolute, out var uri))
//                 url = uri.ToString().TrimEnd('/');
//         }
//
//         return (url, model);
//     }
//
//     /// <summary>返回第一个非空白候选值；全部为空时返回 <paramref name="fallback"/>。</summary>
//     public static string Coalesce(string fallback, params string?[] candidates)
//     {
//         foreach (var c in candidates)
//             if (!string.IsNullOrWhiteSpace(c)) return c!;
//         return fallback;
//     }
// }
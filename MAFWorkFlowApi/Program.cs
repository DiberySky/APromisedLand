using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.HealthChecks;
using MAFWorkFlowApi.Infrastructure;
using MAFWorkFlowApi.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OllamaSharp;
using AgentSessionStore = Microsoft.Agents.AI.Hosting.AgentSessionStore;

#pragma warning disable EXTEXP0001

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Aspire 默认
// ---------------------------------------------------------------------------
builder.AddServiceDefaults();

// ---------------------------------------------------------------------------
// 2. 工具调用缓存
// ---------------------------------------------------------------------------
builder.Services.AddMemoryCache();

// ---------------------------------------------------------------------------
// 3. Redis 分布式缓存
// ---------------------------------------------------------------------------
builder.AddRedisDistributedCache("Redis");

// ---------------------------------------------------------------------------
// 4. 全局 HttpClient 配置
// ---------------------------------------------------------------------------
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.RemoveAllResilienceHandlers();
    http.ConfigureHttpClient(client =>
        client.Timeout = TimeSpan.FromMinutes(10));
});

builder.Services.AddHttpClient(OllamaModelReadyHealthCheck.HttpClientName)
    .ConfigureHttpClient(client =>
        client.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddHttpClient(OllamaWarmupService.HttpClientName)
    .ConfigureHttpClient(client =>
        client.Timeout = TimeSpan.FromMinutes(10));

// ★ 合并：Reranker HttpClient 只注册一次
builder.Services.AddHttpClient("Reranker")
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromMinutes(3);
    });

// ---------------------------------------------------------------------------
// 5. OllamaSharp 客户端
// ---------------------------------------------------------------------------
var embeddingModelName = builder.Configuration["Embedding:Model"] ?? "bge-m3";
var chatModelName      = builder.Configuration["Chat:Model"]      ?? "qwen3:8b";

Console.WriteLine($"[Config] Embedding Model = {embeddingModelName}");
Console.WriteLine($"[Config] Chat Model      = {chatModelName}");

builder.AddOllamaApiClient("chat-model")
    .AddKeyedChatClient("chat-model");

builder.AddOllamaApiClient("embedding")
    .AddKeyedEmbeddingGenerator("embedding");

// 覆盖 keyed embedding generator（确保用配置里的模型名）
builder.Services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
    "embedding",
    (sp, _) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var connectionString = config.GetConnectionString("embedding")
                               ?? throw new InvalidOperationException(
                                   "ConnectionStrings:embedding 未配置。" +
                                   "检查 AppHost 中是否 .WithReference(context.Embedding)。");

        var uri   = ParseOllamaEndpoint(connectionString);
        var model = config["Embedding:Model"] ?? "bge-m3";

        Console.WriteLine($"[Embedding] endpoint={uri}, model={model}");

        return new OllamaApiClient(uri, model);
    });

// 桥接：keyed → non-keyed
builder.Services.AddSingleton<IChatClient>(sp =>
    sp.GetRequiredKeyedService<IChatClient>("chat-model"));

// ---------------------------------------------------------------------------
// 6. MVC + OpenAPI
// ---------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new NameValueCollectionConverter());
    });
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// ---------------------------------------------------------------------------
// 7. Agent 配置绑定
// ---------------------------------------------------------------------------
builder.Services
    .AddOptions<OllamaAgentOptions>()
    .Bind(builder.Configuration.GetSection(OllamaAgentOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ---------------------------------------------------------------------------
// 8. 会话存储与 MAF 服务
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<RedisAgentSessionStore>();
builder.Services.AddSingleton<AgentSessionStore>(
    sp => sp.GetRequiredService<RedisAgentSessionStore>());
builder.Services.AddSingleton<IConversationCatalog>(
    sp => sp.GetRequiredService<RedisAgentSessionStore>());
builder.Services.AddSingleton<MafAgentService>();

// ---------------------------------------------------------------------------
// 9. Ollama 预热
// ---------------------------------------------------------------------------
builder.Services.AddHostedService<OllamaWarmupService>();

// ---------------------------------------------------------------------------
// 10. 健康检查
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddCheck<OllamaModelReadyHealthCheck>(
        name: "ollama-model-ready",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Delay   = TimeSpan.FromSeconds(30);
    options.Period  = TimeSpan.FromSeconds(30);
    options.Timeout = TimeSpan.FromSeconds(10);
});

// ---------------------------------------------------------------------------
// 11. LiteGraph SDK + 图数据服务
// ---------------------------------------------------------------------------
builder.Services.AddLiteGraph(builder.Configuration);

builder.Services.AddSingleton<LiteGraph.Sdk.LiteGraphSdk>(sp =>
{
    var options = sp.GetRequiredService<IOptions<LiteGraphOptions>>();
    var logger  = sp.GetRequiredService<ILogger<LiteGraphRestClient>>();

    // ★ 复用同一个 Resolver，避免两条路径不一致
    var endpoint = LiteGraphEndpointResolver.Resolve(options.Value, logger);

    return new LiteGraph.Sdk.LiteGraphSdk(endpoint, "default");
});

// ---------------------------------------------------------------------------
// 12. Graph Function Calling Agent（Scoped）
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ToolCallContext>();
builder.Services.AddScoped<GraphTools>();
builder.Services.AddScoped<GraphAgentService>();
builder.Services.AddScoped<AssistantAgentService>();
builder.Services.AddScoped<LlmAgentRouter>();

// ---------------------------------------------------------------------------
// 13. MCP Server
// ---------------------------------------------------------------------------
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<McpGraphTools>();

builder.Services.AddScoped<GraphExportService>();
builder.Services.AddScoped<IntentParserService>();

// ══════════════════════════════════════════════════════════
// ★ 14. RerankerService — 改为 Singleton
//   原因：
//    - 无状态、只读 IConfiguration
//    - 依赖的 IHttpClientFactory / IConfiguration / ILogger 均 Singleton 安全
//    - 原 Scoped 注册导致每个 HTTP 请求都重建并打印初始化日志（日志刷屏）
// ══════════════════════════════════════════════════════════
builder.Services.AddSingleton<RerankerService>();

var app = builder.Build();

// ---------------------------------------------------------------------------
// ★ 15. 中间件管线（顺序已修正）
//    ★ LiteGraphExceptionMiddleware 必须放在最外层：
//      1) 先于 DeveloperExceptionPage 捕获 HttpRequestException，避免堆栈泄漏
//      2) 下游任何中间件抛出的 HTTP 异常都会被统一映射为 JSON
// ---------------------------------------------------------------------------
app.UseMiddleware<LiteGraphExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

app.UseMiddleware<McpApiKeyMiddleware>();
app.UseRouting();
app.MapControllers();
app.MapDefaultEndpoints();
app.MapMcp("/mcp");

app.Run();

// ══════════════════════════════════════════════════════════
// 辅助：解析 Ollama 连接字符串为 Uri
// ══════════════════════════════════════════════════════════
static Uri ParseOllamaEndpoint(string connectionString)
{
    var parts = connectionString.Split(
        ';',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    foreach (var part in parts)
    {
        var eq = part.IndexOf('=');
        if (eq > 0)
        {
            var key = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            if (key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase))
                return new Uri(value);
        }
    }
    return new Uri(connectionString);
}

#pragma warning restore EXTEXP0001
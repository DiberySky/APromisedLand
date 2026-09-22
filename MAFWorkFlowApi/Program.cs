using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.HealthChecks;
using MAFWorkFlowApi.Infrastructure;
using MAFWorkFlowApi.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OllamaSharp;
using AgentSessionStore = Microsoft.Agents.AI.Hosting.AgentSessionStore;

#pragma warning disable EXTEXP0001

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Aspire 默认
// ---------------------------------------------------------------------------
builder.AddServiceDefaults();

// ══════════════════════════════════════════════════════════
// ★ 工具调用缓存（相同参数 5 分钟内复用结果）
// ══════════════════════════════════════════════════════════
builder.Services.AddMemoryCache();

// ---------------------------------------------------------------------------
// 2. Redis 分布式缓存
// ---------------------------------------------------------------------------
builder.AddRedisDistributedCache("Redis");

// ---------------------------------------------------------------------------
// 3. 全局 HttpClient 配置
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

// ---------------------------------------------------------------------------
// 4. OllamaSharp 客户端
// ══════════════════════════════════════════════════════════
// ★ 从配置读取模型名（由 AppHost 通过 WithEnvironment 注入）
//    - 环境变量：Embedding__Model / Chat__Model
//    - 启动时日志打印，方便确认
// ══════════════════════════════════════════════════════════
var embeddingModelName = builder.Configuration["Embedding:Model"] ?? "bge-large";
var chatModelName      = builder.Configuration["Chat:Model"]      ?? "qwen3:8b";

// 早日志（用 Console，因为 ILogger 还没建好）
Console.WriteLine($"[Config] Embedding Model = {embeddingModelName}");
Console.WriteLine($"[Config] Chat Model      = {chatModelName}");

builder.AddOllamaApiClient("chat-model")
    .AddKeyedChatClient("chat-model");

builder.AddOllamaApiClient("embedding")
    .AddKeyedEmbeddingGenerator("embedding");

// ★ 覆盖 keyed embedding generator：确保用配置里的模型名
//   （覆盖 AddKeyedEmbeddingGenerator 的默认注册）
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
        var model = config["Embedding:Model"] ?? "bge-large";

        Console.WriteLine($"[Embedding] endpoint={uri}, model={model}");

        // ★ 修正：OllamaSharp 没有 OllamaEmbeddingGenerator，
        //   OllamaApiClient 同时实现 IChatClient + IEmbeddingGenerator
        return new OllamaApiClient(uri, model);
    });

// ★ 桥接：keyed → non-keyed
builder.Services.AddSingleton<IChatClient>(sp =>
    sp.GetRequiredKeyedService<IChatClient>("chat-model"));

// ---------------------------------------------------------------------------
// 5. MVC + OpenAPI
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
// 6. Agent 配置绑定
// ---------------------------------------------------------------------------
builder.Services
    .AddOptions<OllamaAgentOptions>()
    .Bind(builder.Configuration.GetSection(OllamaAgentOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ---------------------------------------------------------------------------
// 7. 会话存储与 MAF 服务
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<RedisAgentSessionStore>();
builder.Services.AddSingleton<AgentSessionStore>(
    sp => sp.GetRequiredService<RedisAgentSessionStore>());
builder.Services.AddSingleton<IConversationCatalog>(
    sp => sp.GetRequiredService<RedisAgentSessionStore>());
builder.Services.AddSingleton<MafAgentService>();

// ---------------------------------------------------------------------------
// 8. 启动时预热 Ollama 模型
// ---------------------------------------------------------------------------
builder.Services.AddHostedService<OllamaWarmupService>();

// ---------------------------------------------------------------------------
// 9. 健康检查
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
// 10. LiteGraph SDK + 图数据服务
// ---------------------------------------------------------------------------
builder.Services.AddLiteGraph(builder.Configuration);

builder.Services.AddSingleton<LiteGraph.Sdk.LiteGraphSdk>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["LiteGraph:Endpoint"] ?? "http://localhost:8701";
    return new LiteGraph.Sdk.LiteGraphSdk(endpoint, "default");
});

// ---------------------------------------------------------------------------
// 11. Graph Function Calling Agent（Scoped）
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ToolCallContext>();
builder.Services.AddScoped<GraphTools>();
builder.Services.AddScoped<GraphAgentService>();
builder.Services.AddScoped<AssistantAgentService>();
builder.Services.AddScoped<LlmAgentRouter>();

// ---------------------------------------------------------------------------
// 12. MCP Server
// ---------------------------------------------------------------------------
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<McpGraphTools>();

builder.Services.AddScoped<GraphExportService>();

builder.Services.AddScoped<IntentParserService>();

var app = builder.Build();

// ---------------------------------------------------------------------------
// 13. 中间件管线
// ---------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

app.UseMiddleware<LiteGraphExceptionMiddleware>();
app.UseMiddleware<McpApiKeyMiddleware>();
app.UseRouting();
app.MapControllers();
app.MapDefaultEndpoints();
app.MapMcp("/mcp");

app.Run();

// ══════════════════════════════════════════════════════════
// 辅助：解析 Ollama 连接字符串为 Uri
//   可能格式：
//     - "http://host:port"
//     - "Endpoint=http://host:port;Model=xxx"
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
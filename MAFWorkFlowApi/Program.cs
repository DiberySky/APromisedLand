using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.HealthChecks;
using MAFWorkFlowApi.Infrastructure;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
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

// 启动期日志：在 DI 容器构建前独立创建一个 LoggerFactory
using var startupLoggerFactory = LoggerFactory.Create(lb => lb.AddConsole());
var startupLog = startupLoggerFactory.CreateLogger("Startup");

var embeddingModelName = builder.Configuration["Embedding:Model"] ?? "bge-m3";
var chatModelName      = builder.Configuration["Chat:Model"]      ?? "qwen3:8b";

startupLog.LogInformation("Embedding Model = {Model}", embeddingModelName);
startupLog.LogInformation("Chat Model      = {Model}", chatModelName);

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
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Embedding");
        var connectionString = config.GetConnectionString("embedding")
                               ?? throw new InvalidOperationException(
                                   "ConnectionStrings:embedding 未配置。" +
                                   "检查 AppHost 中是否 .WithReference(context.Embedding)。");

        var endpoint = OllamaEndpointResolver.FromConnectionString(connectionString)
                       ?? connectionString;
        var uri = new Uri(endpoint);
        var model = config["Embedding:Model"] ?? "bge-m3";

        logger.LogInformation("Embedding endpoint={Endpoint}, model={Model}", uri, model);

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
// 7. Agent / 图领域 / MCP + 健康检查（模块化注册）
// ---------------------------------------------------------------------------
builder.Services.AddMafAgentServices(builder.Configuration);
builder.Services.AddGraphDomainServices(builder.Configuration);
builder.Services.AddMcpAndHealthChecks();

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

#pragma warning restore EXTEXP0001
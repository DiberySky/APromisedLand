using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.HealthChecks;
using MAFWorkFlowApi.Infrastructure;
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
// ★ 工具调用缓存（相同参数 60 秒内复用结果）
// ══════════════════════════════════════════════════════════
builder.Services.AddMemoryCache();

// ---------------------------------------------------------------------------
// 2. Redis 分布式缓存
// ---------------------------------------------------------------------------
builder.AddRedisDistributedCache("Redis");

// ---------------------------------------------------------------------------
// 3. 全局 HttpClient 配置
//    - 移除 Aspire 默认弹性管道（AttemptTimeout=10s 会截断 Ollama 长推理）
//    - 全局超时放宽到 10 分钟
// ---------------------------------------------------------------------------
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.RemoveAllResilienceHandlers();
    http.ConfigureHttpClient(client =>
        client.Timeout = TimeSpan.FromMinutes(10));
});

// 健康检查专用：短超时，避免拖慢 /health
builder.Services.AddHttpClient(OllamaModelReadyHealthCheck.HttpClientName)
    .ConfigureHttpClient(client =>
        client.Timeout = TimeSpan.FromSeconds(10));

// 预热专用：长超时，用于启动时加载模型
builder.Services.AddHttpClient(OllamaWarmupService.HttpClientName)
    .ConfigureHttpClient(client =>
        client.Timeout = TimeSpan.FromMinutes(10));

// ---------------------------------------------------------------------------
// 4. OllamaSharp 客户端
//    CommunityToolkit 扩展内部会注册 keyed IChatClient / IEmbeddingGenerator
// ---------------------------------------------------------------------------
builder.AddOllamaApiClient("chat-model")
    .AddKeyedChatClient("chat-model");

builder.AddOllamaApiClient("embedding")
    .AddKeyedEmbeddingGenerator("embedding");

// ★ 桥接：keyed → non-keyed
//    MafAgentService / GraphAgentService 使用 [FromKeyedServices("chat-model")]
//    DI 验证阶段会尝试按 non-keyed 解析一次，因此必须提供 non-keyed 版本。
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

// ---------------------------------------------------------------------------
// 10. LiteGraph SDK + 图数据服务
//     注意：扩展方法接收者是 IServiceCollection，因此用 builder.Services
// ---------------------------------------------------------------------------
builder.Services.AddLiteGraph(builder.Configuration);

// ══════════════════════════════════════════════════════════
// ★ Graph Function Calling Agent（Scoped：工具调用上下文按请求隔离）
// ══════════════════════════════════════════════════════════
builder.Services.AddScoped<ToolCallContext>();
builder.Services.AddScoped<GraphTools>();
builder.Services.AddScoped<GraphAgentService>();
builder.Services.AddScoped<AssistantAgentService>();
builder.Services.AddScoped<LlmAgentRouter>();

// ══════════════════════════════════════════════════════════
// ★ MCP Server：把 GraphTools 暴露给外部 AI 客户端
// ══════════════════════════════════════════════════════════
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<McpGraphTools>();

var app = builder.Build();

// ---------------------------------------------------------------------------
// 11. 中间件管线
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

// 在 app.UseRouting() 之前添加异常中间件：
app.UseMiddleware<LiteGraphExceptionMiddleware>();

app.UseRouting();
app.MapControllers();
app.MapDefaultEndpoints();

// ★ 映射 MCP 端点（外部 AI 客户端连接此地址）
app.MapMcp("/mcp");

app.Run();

#pragma warning restore EXTEXP0001
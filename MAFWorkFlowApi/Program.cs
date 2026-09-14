using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.HealthChecks;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

#pragma warning disable EXTEXP0001

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Aspire 默认
// ---------------------------------------------------------------------------
builder.AddServiceDefaults();

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
// ---------------------------------------------------------------------------
builder.AddOllamaApiClient("chat-model")
    .AddKeyedChatClient("chat-model");

builder.AddOllamaApiClient("embedding")
    .AddKeyedEmbeddingGenerator("embedding");

// ---------------------------------------------------------------------------
// 5. MVC + OpenAPI
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
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

var app = builder.Build();

// ---------------------------------------------------------------------------
// 10. 中间件管线
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

app.UseRouting();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

#pragma warning restore EXTEXP0001
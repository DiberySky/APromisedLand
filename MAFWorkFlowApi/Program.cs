using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.HealthChecks;
using Microsoft.Agents.AI.Hosting; 
using Microsoft.Extensions.Diagnostics.HealthChecks;

#pragma warning disable EXTEXP0001

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Aspire 默认：OpenTelemetry、健康检查、服务发现、HTTP 容错
// ---------------------------------------------------------------------------
builder.AddServiceDefaults();

// ---------------------------------------------------------------------------
// 2. Redis 分布式缓存（用于 AgentSession 持久化）
//    资源名 "Redis" 与 AppHost 中 RedisExtension.cs 的声明一致
// ---------------------------------------------------------------------------
builder.AddRedisDistributedCache("Redis");

// ---------------------------------------------------------------------------
// 3. 全局配置 HttpClient：移除 Polly 弹性管道，设置长超时
//    Ollama 推理是长事务，默认 30s 超时会截断请求
// ---------------------------------------------------------------------------
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.RemoveAllResilienceHandlers();
    http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(5));
});

// ---------------------------------------------------------------------------
// 4. OllamaSharp 客户端集成
// ---------------------------------------------------------------------------
builder.AddOllamaApiClient("chat-model")
    .AddKeyedChatClient("chat-model");

builder.AddOllamaApiClient("embedding")
    .AddKeyedEmbeddingGenerator("embedding");

// ---------------------------------------------------------------------------
// 5. Controller 风格 MVC + OpenAPI
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// ---------------------------------------------------------------------------
// 6. 绑定 Agent 配置
// ---------------------------------------------------------------------------
builder.Services
    .AddOptions<OllamaAgentOptions>()
    .Bind(builder.Configuration.GetSection(OllamaAgentOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ---------------------------------------------------------------------------
// 7. 注册会话存储（Redis 实现）与 MAF 封装服务
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<AgentSessionStore, RedisAgentSessionStore>();
builder.Services.AddSingleton<MafAgentService>();

// ---------------------------------------------------------------------------
// 8. 健康检查
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddCheck<OllamaModelReadyHealthCheck>(
        name: "ollama-model-ready",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

var app = builder.Build();

// ---------------------------------------------------------------------------
// 9. 中间件管线
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
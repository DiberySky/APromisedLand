using TreeGraph.MafWfApi.Agents;
using TreeGraph.MafWfApi.Infrastructure;
using TreeGraph.MafWfApi.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

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

// ---------------------------------------------------------------------------
// 5. vLLM 端点解析与 IChatClient 装配
//    优先级：VLLM_HTTP 环境变量 > Agent:Endpoint > http://localhost:8000
// ---------------------------------------------------------------------------
var vllmEndpoint = ResolveVllmEndpoint(builder.Configuration);

// 启动期日志：在 DI 容器构建前独立创建一个 LoggerFactory
using var startupLoggerFactory = LoggerFactory.Create(lb => lb.AddConsole());
var startupLog = startupLoggerFactory.CreateLogger("Startup");
startupLog.LogInformation("vLLM Endpoint = {Endpoint}", vllmEndpoint);
startupLog.LogInformation("Chat Model    = {Model}",
    builder.Configuration["Agent:ChatModel"] ?? "qwen3-4b-awq");

// 供 HealthController /api/health/deep 探活使用（基址为端点根，调用 "v1/models"）
builder.Services.AddHttpClient("vllm", client =>
{
    client.BaseAddress = new Uri(vllmEndpoint.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromMinutes(1);
});

// IChatClient：VllmChatClient（注入 enable_thinking=false）+ ThinkStripping 包装
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var opt = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    var http = new HttpClient
    {
        BaseAddress = new Uri(vllmEndpoint.TrimEnd('/') + "/v1/"),
        Timeout = System.Threading.Timeout.InfiniteTimeSpan
    };

    var vllm = new VllmChatClient(
        http, opt.ChatModel, opt.ApiKey,
        loggerFactory.CreateLogger<VllmChatClient>());

    return new ThinkStrippingChatClient(
        vllm, loggerFactory.CreateLogger<ThinkStrippingChatClient>());
});

// ---------------------------------------------------------------------------
// 6. MVC + OpenAPI
// ---------------------------------------------------------------------------
builder.Services.AddControllers(options =>
    {
        // ★ 全局 ApiResponse 信封（排除 HealthController；SSE 流式端点不经 ObjectResult 不受影响）
        options.Filters.Add<ApiEnvelopeFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new NameValueCollectionConverter());
    });
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// ---------------------------------------------------------------------------
// 7. Agent / 图领域 / MCP（模块化注册）
// ---------------------------------------------------------------------------
builder.Services.AddMafAgentServices(builder.Configuration);
builder.Services.AddGraphDomainServices(builder.Configuration);
builder.Services.AddMcpServices();

var app = builder.Build();

// ---------------------------------------------------------------------------
// 8. 中间件管线（顺序与旧 MAFWorkFlowApi 一致）
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

app.UseMiddleware<McpApiKeyMiddleware>();
app.UseRouting();
app.MapControllers();
app.MapDefaultEndpoints();
app.MapMcp("/mcp");

app.Run();

#pragma warning restore EXTEXP0001

// ---------------------------------------------------------------------------
// 本地函数
// ---------------------------------------------------------------------------
static string ResolveVllmEndpoint(IConfiguration config)
{
    var fromEnv = Environment.GetEnvironmentVariable("VLLM_HTTP");
    if (!string.IsNullOrWhiteSpace(fromEnv))
        return fromEnv.Trim().TrimEnd('/');

    var fromConfig = config["Agent:Endpoint"];
    if (!string.IsNullOrWhiteSpace(fromConfig))
        return fromConfig.Trim().TrimEnd('/');

    return "http://localhost:8000";
}

// ★ 供 WebApplicationFactory<Program> 引用（集成测试需要 public）
public partial class Program;

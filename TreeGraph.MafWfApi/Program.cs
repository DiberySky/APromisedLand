using TreeGraph.MafWfApi.Agents;
using TreeGraph.MafWfApi.Infrastructure;
using TreeGraph.MafWfApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using TreeGraph.Shared.Identity;

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
// 3.1 身份与访问管理：JwtBearer + ICurrentUser（/mcp 仍由 McpApiKeyMiddleware 保护）
// ---------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.TokenValidationParameters.ValidAudiences = new[] { builder.Configuration["Keycloak:Audience"] }
            .Concat(builder.Configuration.GetSection("Keycloak:AdditionalAudiences").Get<string[]>() ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool>("Keycloak:RequireHttpsMetadata");
        options.TokenValidationParameters.RoleClaimType = IdentityClaimTypes.Roles;
        options.TokenValidationParameters.NameClaimType = IdentityClaimTypes.PreferredUsername;
        // 关闭 inbound claim 类型映射：Keycloak 的 sub/roles 短名会被默认映射为 WS-Federation 长名，
        // 导致 FindFirst("sub") 与 RoleClaimType="roles" 失效（401/403）。保持短名以匹配 IdentityClaimTypes。
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sub = context.Principal?.FindFirst(IdentityClaimTypes.Subject)?.Value;
                if (string.IsNullOrWhiteSpace(sub))
                    return;

                try
                {
                    var cache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
                    var revoked = await cache.GetStringAsync(IdentityConstants.RevocationKeyPrefix + sub);
                    if (!string.IsNullOrEmpty(revoked))
                        context.Fail("账号已被禁用");
                }
                catch (Exception)
                {
                    // 撤销存储不可达时不阻断请求，等待下次校验。
                }
            }
        };
    });
builder.Services.AddAuthorization();
var wasmOrigins = builder.Configuration.GetSection("Cors:WasmOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("TreeGraphWasm", policy =>
    policy.WithOrigins(wasmOrigins)
        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
        .WithHeaders("Authorization", "Content-Type")
        .WithExposedHeaders("Content-Length", "Content-Disposition")
        .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

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

app.UseCors("TreeGraphWasm");
app.UseMiddleware<McpApiKeyMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRouting();
var controllers = app.MapControllers();
if (app.Configuration.GetValue<bool>("Identity:RequireAuthenticatedApi"))
    controllers.RequireAuthorization();
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

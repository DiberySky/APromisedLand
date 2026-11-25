using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using TreeGraph.Persistence.Data;
using TreeGraph.Shared.Identity;
using TreeGraph.SyncServices.GraphSync;
using TreeGraph.SyncServices.Messaging;
using TreeGraph.SyncServices.SemanticSearch;
using TreeGraph.WebShared.Infrastructure;
using TreeGraph.WebShared.NodeEavSky;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

// ★ Aspire ServiceDefaults：服务发现、健康检查、OpenTelemetry
builder.AddServiceDefaults();
builder.AddRedisDistributedCache("Redis");
builder.Services.AddHttpContextAccessor();

// 双跑期间接受旧 Web 与 WASM audience；认证仅填充用户上下文，不全局改变匿名 API 行为。
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
                if (string.IsNullOrWhiteSpace(sub)) return;

                try
                {
                    var cache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
                    var revoked = await cache.GetStringAsync(IdentityConstants.RevocationKeyPrefix + sub);
                    if (!string.IsNullOrEmpty(revoked)) context.Fail("账号已被禁用");
                }
                catch (Exception)
                {
                    // 与既有业务 API 双跑策略一致：撤销存储暂不可达时不阻断旧客户端。
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
        .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

builder.Services.AddControllers(options =>
    {
        // NodeEavSky 全局 ApiResponse 信封（与核心 API 对齐）
        options.Filters.Add<ApiEnvelopeFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new DynamicCompositeValueJsonConverter());
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddMemoryCache();

// ★ 共享 TreeGraphDbContext（只读消费：同步器读 PG 数据做 Nebula/Qdrant 投影源）
builder.AddNpgsqlDbContext<TreeGraphDbContext>(
    "TreeGraphDb",
    configureDbContextOptions: options =>
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

// ★ 图同步管道（PostgreSQL → NebulaGraph，经 TreeGraph.NebulaFastApi REST 网关）
builder.Services.AddGraphSync(builder.Configuration);

// ★ 语义搜索 + 向量同步管道（Qdrant + vLLM bge-m3）
builder.Services.AddSemanticSearch(builder.Configuration);

// ★ Wolverine 消息总线（消费端）：监听 RabbitMQ 队列，接收核心 API 发布的变更事件。
//   Wolverine 按约定自动发现本程序集中的 *Consumer handler（向量同步 + 图同步）。
builder.Host.UseWolverine(opts => opts.UseSyncServicesMessaging(builder.Configuration));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseCors("TreeGraphWasm");
app.UseAuthentication();
app.UseAuthorization();
var controllers = app.MapControllers();
if (app.Configuration.GetValue<bool>("Identity:RequireAuthenticatedApi"))
    controllers.RequireAuthorization();

// ★ Aspire 默认端点（/health、/alive），供 Dashboard 探测
app.MapDefaultEndpoints();

app.Run();

// ★ 让 WebApplicationFactory<Program> 可引用（集成测试）
public partial class Program { }

using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Distributed;
using TreeGraph.FileStorageApi.Data;
using TreeGraph.Shared.Identity;
using TreeGraph.FileStorageApi.Files;
using TreeGraph.FileStorageApi.Security;
using TreeGraph.FileStorageApi.Storage;
using TreeGraph.FileStorageApi.Uploads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;


var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache("Redis");

// 双跑期间接受旧 Web 与 WASM audience；不全局要求认证，保持既有 API 契约。
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

// ── 调用者上下文 ──
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICallerContext, HttpCallerContext>();

// ── 数据库 ──
// ★ 方案 A：禁用 Aspire 默认的 NpgsqlRetryingExecutionStrategy。
//   CompleteAsync 4.7 节使用显式事务（BeginTransactionAsync），
//   与 EF Core 的 RetryingExecutionStrategy 冲突。
//
// ★ 命令超时提高到 5 分钟：
//   1 GB 文件 ≈ 127 个 8 MB 分片，DELETE/INSERT 涉及大量 bytea。
//   默认 30s 会触发 Npgsql 超时，被误判为瞬时故障。
//   （分批删除已在 FileUploadService 中进一步缓解）
builder.AddNpgsqlDbContext<FileStorageContext>(
    "FileMetadataDb",
    configureSettings: settings =>
    {
        settings.DisableRetry  = true;
        settings.CommandTimeout = 300;   // 秒
    });

// ── SeaweedFS S3 ──
builder.Services
    .AddOptions<ObjectStorageOptions>()
    .Bind(builder.Configuration.GetSection(ObjectStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var o = sp.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
    var cfg = new AmazonS3Config
    {
        ServiceURL           = o.Endpoint,
        ForcePathStyle       = o.ForcePathStyle,
        UseHttp              = o.UseHttp,
        AuthenticationRegion = o.Region,

        // ★ multipart 上传单片可能数十秒，AWS SDK 默认 100s 不够。
        //   v4 中 ReadWriteTimeout 已移除，Timeout 是唯一的请求超时。
        Timeout       = TimeSpan.FromMinutes(10),
        MaxErrorRetry = 3,
    };
    return new AmazonS3Client(o.AccessKey, o.SecretKey, cfg);
});

builder.Services.AddSingleton<IObjectStorage, S3ObjectStorage>();

// ── 业务服务 ──
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IFileMetadataService, FileMetadataService>();

// ★ 审计队列 + 后台写入（替代 FileMetadataService 中无界 Task.Run）
builder.Services.AddSingleton<AuditQueue>();
builder.Services.AddHostedService<AuditWriterService>();

// ★ UploadCleanup 配置校验，拼写错误 FailFast
builder.Services
    .AddOptions<UploadCleanupOptions>()
    .Bind(builder.Configuration.GetSection(UploadCleanupOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<UploadSessionCleanupService>();

// ── CORS：旧前端保留 credentials；WASM 使用独立、精确且无 credentials 的策略 ──
var wasmOrigins = builder.Configuration.GetSection("Cors:WasmOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? ["http://localhost:5173"];

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
    options.AddPolicy("TreeGraphWasm", policy =>
        policy.WithOrigins(wasmOrigins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .WithHeaders("Authorization", "Content-Type", "Range", "X-Chunk-Sha256")
            .WithExposedHeaders("Content-Length", "Content-Range", "Content-Disposition", "Accept-Ranges", "ETag")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// ══════════════════════════════════════════════════════════
// ★ 健康检查：延迟启动 + 降低频率
//   避免应用启动期间 DB 还没就绪时被 startup probe 取消，
//   导致 FileStorageContext 健康检查误报 Unhealthy。
// ══════════════════════════════════════════════════════════
builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Delay   = TimeSpan.FromSeconds(30);   // 启动后 30 秒再检查（避开清理服务启动和EF迁移）
    options.Period  = TimeSpan.FromSeconds(30);   // 每 30 秒一次
    options.Timeout = TimeSpan.FromSeconds(60);   // 单次检查放宽到 60 秒超时
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FileStorageContext>();
    await db.Database.MigrateAsync();
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

app.UseWhen(
    context => wasmOrigins.Contains(context.Request.Headers.Origin.ToString(), StringComparer.Ordinal),
    branch => branch.UseCors("TreeGraphWasm"));
app.UseWhen(
    context => !wasmOrigins.Contains(context.Request.Headers.Origin.ToString(), StringComparer.Ordinal),
    branch => branch.UseCors("Frontend"));
app.UseAuthentication();
app.UseAuthorization();

// 全局捕获 InvalidTenantException → 400（BadHttpRequestException 从服务层抛出时变 500）
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (InvalidTenantException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "InvalidTenant",
            status = 400,
            detail = ex.Message,
        });
    }
});

app.UseRouting();
var controllers = app.MapControllers();
if (app.Configuration.GetValue<bool>("Identity:RequireAuthenticatedApi"))
    controllers.RequireAuthorization();
app.MapDefaultEndpoints();

app.Run();
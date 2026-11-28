using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using TreeGraph.IdentityAdmin.Api.Endpoints;
using TreeGraph.IdentityAdmin.Api.Infrastructure;
using TreeGraph.IdentityAdmin.Backup;
using TreeGraph.IdentityAdmin.Extensions;
using TreeGraph.Shared.Identity;
using TreeGraph.Shared.Identity.Admin;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache("Redis");
builder.Services.AddHttpContextAccessor();
builder.Services.AddIdentityAdmin(builder.Configuration);
builder.Services.AddScoped<IAdminActorAccessor, HttpContextAdminActorAccessor>();

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TreeGraph",
        "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("TreeGraph")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));
builder.Services.AddSingleton<IBackupProtector, DataProtectionBackupProtector>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"] ?? "treegraph-wasm";
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
                {
                    context.Fail("令牌缺少 subject");
                    return;
                }

                try
                {
                    var cache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
                    var revoked = await cache.GetStringAsync(IdentityConstants.RevocationKeyPrefix + sub);
                    if (!string.IsNullOrEmpty(revoked))
                    {
                        var issuedAtValue = context.Principal?.FindFirst(IdentityClaimTypes.IssuedAt)?.Value;
                        if (!long.TryParse(revoked, out var revokedAt)
                            || !long.TryParse(issuedAtValue, out var issuedAt)
                            || issuedAt <= revokedAt)
                            context.Fail("令牌已在密码重置、下线或禁用操作时失效");
                    }
                }
                catch (Exception)
                {
                    context.Fail("无法校验账号撤销状态");
                }
            }
        };
    });
builder.Services.AddAuthorization(options =>
    options.AddPolicy(IdentityConstants.AdminOnlyPolicy, policy =>
        policy.RequireRole(IdentityRoles.Superuser, IdentityRoles.Admin, "app_admin")));

var wasmOrigins = builder.Configuration.GetSection("Cors:WasmOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("TreeGraphWasm", policy =>
    policy.WithOrigins(wasmOrigins)
        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
        .WithHeaders("Authorization", "Content-Type")
        .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("IdentityAdmin:EnableDatabaseMigration"))
    await app.Services.MigrateIdentityAdminDatabaseAsync();

app.UseExceptionHandler();
app.UseCors("TreeGraphWasm");
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapIdentityAdminApi();
app.Run();

public partial class Program { }

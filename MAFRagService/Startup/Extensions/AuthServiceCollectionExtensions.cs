using System.Text.Json;

namespace MAFRagService.Startup.Extensions;

/// <summary>
/// 开发阶段：仅注册 MVC 管线，不启用任何身份验证。
/// 控制器不再标注 [Authorize]，所有请求默认放行。
///
/// <para><b>生产环境恢复步骤：</b></para>
/// <list type="number">
///   <item>恢复下方注释中的 JWT Bearer 配置。</item>
///   <item>Program.cs 中把 <c>AddRagApi()</c> 改回 <c>AddRagAuth(config, env)</c>，
///         并恢复 <c>UseAuthentication()</c> / <c>UseAuthorization()</c>。</item>
///   <item>恢复各 Controller 顶部的 <c>[Authorize]</c> 标注。</item>
///   <item>恢复 <c>BaseApiController.ResolveTenant</c> 中从 JWT claim 读取的逻辑。</item>
///   <item>恢复 <c>HangfireDashboardAuthFilter</c> 的身份校验。</item>
///   <item>恢复 <c>JwtOptions</c> 与 <c>appsettings</c> 中的 <c>Jwt</c> 节。</item>
/// </list>
/// </summary>
public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddRagApi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services
            .AddControllers()
            .AddJsonOptions(o =>
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

        return services;
    }

    // ============================================================
    // 生产环境恢复用：以下是原 JWT Bearer 配置，按需启用。
    // 同时在 Program.cs 中恢复 UseAuthentication / UseAuthorization。
    // ============================================================
    //
    // public static IServiceCollection AddRagAuth(
    //     this IServiceCollection services,
    //     IConfiguration config,
    //     IHostEnvironment env)
    // {
    //     // Development 与 Production 都要求显式配置；不再有硬编码回退。
    //     var secret = config["Jwt:SecretKey"];
    //
    //     if (string.IsNullOrWhiteSpace(secret))
    //     {
    //         throw new InvalidOperationException(
    //             "缺少配置 Jwt:SecretKey。" +
    //             "Development 请写入 appsettings.Development.json 或 user-secrets；" +
    //             "Production 请通过环境变量 Jwt__SecretKey 或 appsettings.Production.json 注入。");
    //     }
    //
    //     if (System.Text.Encoding.UTF8.GetByteCount(secret) < 32)
    //     {
    //         throw new InvalidOperationException(
    //             $"Jwt:SecretKey 长度不足 32 字节（当前 {System.Text.Encoding.UTF8.GetByteCount(secret)}），" +
    //             "HMAC-SHA256 安全强度不足。请使用更长的随机密钥。");
    //     }
    //
    //     services
    //         .AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    //         .AddJwtBearer(opt =>
    //         {
    //             opt.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    //             {
    //                 ValidateIssuer           = false,
    //                 ValidateAudience         = false,
    //                 ValidateLifetime         = true,
    //                 ValidateIssuerSigningKey = true,
    //                 IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
    //                     System.Text.Encoding.UTF8.GetBytes(secret))
    //             };
    //         });
    //
    //     services.AddAuthorization();
    //     services.AddHttpContextAccessor();
    //
    //     services
    //         .AddControllers()
    //         .AddJsonOptions(o =>
    //             o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
    //
    //     return services;
    // }
}
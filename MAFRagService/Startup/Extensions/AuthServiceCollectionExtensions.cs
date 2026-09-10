using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace MAFRagService.Startup.Extensions;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddRagAuth(
        this IServiceCollection services,
        IConfiguration config,
        IHostEnvironment env)
    {
        // 允许 Development 下回退到 dev 密钥，生产环境强制要求配置
        var secret = config["Jwt:SecretKey"];
        if (string.IsNullOrEmpty(secret))
        {
            if (!env.IsDevelopment())
                throw new InvalidOperationException(
                    "缺少配置 Jwt:SecretKey，生产环境禁止使用回退密钥。");

            secret = config["Jwt:DevFallback"] ?? "DevSecretKey123!@#";
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = false,
                    ValidateAudience         = false,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(secret))
                };
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();

        services
            .AddControllers()
            .AddJsonOptions(o =>
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

        return services;
    }
}

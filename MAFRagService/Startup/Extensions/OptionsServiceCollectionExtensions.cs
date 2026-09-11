using MAFRagService.Startup.Configuration;
using Microsoft.Extensions.Options;

namespace MAFRagService.Startup.Extensions;

public static class OptionsServiceCollectionExtensions
{
    public static IServiceCollection AddRagOptions(
        this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<OllamaOptions>()
            .Bind(config.GetSection(OllamaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<OllamaOptions>, OllamaOptionsValidator>();

        services
            .AddOptions<SeaweedFsOptions>()
            .Bind(config.GetSection(SeaweedFsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<WeaviateOptions>()
            .Bind(config.GetSection(WeaviateOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // ★ 移除 JwtOptions 注册（开发阶段无身份验证）
        // 生产恢复时加回：
        // services
        //     .AddOptions<JwtOptions>()
        //     .Bind(config.GetSection(JwtOptions.SectionName))
        //     .ValidateDataAnnotations()
        //     .ValidateOnStart();

        services
            .AddOptions<FeatureFlags>()
            .Bind(config.GetSection(FeatureFlags.SectionName))
            .ValidateOnStart();

        return services;
    }
}
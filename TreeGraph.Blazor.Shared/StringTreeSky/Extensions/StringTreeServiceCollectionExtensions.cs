using Microsoft.Extensions.DependencyInjection;
using TreeGraph.Blazor.Shared.StringTreeSky.Services;

namespace TreeGraph.Blazor.Shared.StringTreeSky.Extensions;

public static class StringTreeServiceCollectionExtensions
{
    /// <summary>
    /// 注册独立 StringTreeSky。需宿主持有已配置 BaseAddress 的 HttpClient。
    /// </summary>
    public static IServiceCollection AddStringTreeSky(
        this IServiceCollection services,
        Action<StringTreeSkyOptions>? configure = null)
    {
        var options = new StringTreeSkyOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddScoped<NodeSchemaCache>();
        services.AddScoped<IStringTreeClient, StringTreeApiClient>();
        services.AddScoped<ISpaceClient, SpaceApiClient>();
        services.AddScoped<NodePropertySummaryService>();
        // ViewportService 由 AddResponsive() 提供（宿主 Program.cs 调用）
        return services;
    }
}

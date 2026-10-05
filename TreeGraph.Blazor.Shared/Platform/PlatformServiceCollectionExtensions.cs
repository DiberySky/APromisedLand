using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TreeGraph.Blazor.Shared.Platform;

public static class PlatformServiceCollectionExtensions
{
    /// <summary>
    /// 注册默认 Web 平台上下文（Scoped）。
    /// Hybrid 项目可用 AddSingleton&lt;IPlatformContext, MauiPlatformContext&gt;() 覆盖。
    /// </summary>
    public static IServiceCollection AddTreeGraphPlatform(this IServiceCollection services)
    {
        services.TryAddScoped<IPlatformContext, WebPlatformContext>();
        return services;
    }
}

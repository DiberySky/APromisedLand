using Microsoft.Extensions.DependencyInjection;

namespace TreeGraph.Blazor.Shared.Responsive.Extensions;

public static class ResponsiveServiceCollectionExtensions
{
    /// <summary>
    /// 注册通用响应式模块（ViewportService）。
    /// 任何使用 ResponsiveView / ResponsiveSplit 的宿主都需调用。
    /// </summary>
    public static IServiceCollection AddResponsive(this IServiceCollection services)
    {
        services.AddScoped<ViewportService>();
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TreeGraph.Blazor.Shared.Trees.StringTree.Services;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Extensions;

public static class StringTreeServiceCollectionExtensions
{
    /// <summary>
    /// 注册 StringTreeSky 组件所需服务：
    ///   - IStringTreeActionHandler（默认 Noop，宿主可用 AddScoped 覆盖）
    ///   - StringTreeDialogService
    ///
    /// ★ IStringTreeDataSource 由宿主按业务注册（不在此处 AddScoped）。
    /// ★ 若宿主已 AddMudServices，本方法无额外前置条件。
    /// </summary>
    public static IServiceCollection AddStringTreeSky(this IServiceCollection services)
    {
        // TryAdd：宿主若已注册自定义 Handler，不会被覆盖
        services.TryAddScoped<IStringTreeActionHandler, NoopStringTreeActionHandler>();

        services.AddScoped<StringTreeDialogService>();

        return services;
    }
}

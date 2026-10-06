using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TreeGraph.Blazor.Shared.Trees.StringTree.Services;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Extensions;

public static class StringTreeServiceCollectionExtensions
{
    /// <summary>
    /// 注册旧版（Legacy）StringTree 组件所需服务：
    ///   - IStringTreeActionHandler（默认 Noop，宿主可用 AddScoped 覆盖）
    ///   - StringTreeDialogService
    ///
    /// ★ 更名缘由：与解耦版 StringTreeSky\Extensions 下的 AddStringTreeSky 同名，
    ///   命名空间邻近易致绑定/注册混淆（已实际造成宿主漏注册 Resolver/AttrStats），
    ///   故旧版改为此名以消除歧义。
    /// ★ IStringTreeDataSource 由宿主按业务注册（不在此处 AddScoped）。
    /// ★ 若宿主已 AddMudServices，本方法无额外前置条件。
    /// </summary>
    public static IServiceCollection AddLegacyTreeSky(this IServiceCollection services)
    {
        // TryAdd：宿主若已注册自定义 Handler，不会被覆盖
        services.TryAddScoped<IStringTreeActionHandler, NoopStringTreeActionHandler>();

        services.AddScoped<StringTreeDialogService>();

        return services;
    }
}

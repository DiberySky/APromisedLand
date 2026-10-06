using Microsoft.Extensions.DependencyInjection;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Api.StringTreeSky.Services;

namespace TreeGraph.Api.StringTreeSky.Extensions;

public static class StringTreeApiServiceExtensions
{
    /// <summary>
    /// 注册 StringTreeSky 服务。DbContext 由宿主注册（Aspire AddNpgsqlDbContext("TreeGraphDb")）。
    /// </summary>
    public static IServiceCollection AddStringTreeApi(this IServiceCollection services)
    {
        services.AddScoped<IStringTreeService, EfStringTreeService>();

        // StringTreeNode 属性必须附属于真实 tree node（写入前归属校验）
        services.AddScoped<IEntityOwnerGuard, StringTreeNodeOwnerGuard>();
        return services;
    }
}

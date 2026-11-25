using TreeGraph.Api.Infrastructure;
using TreeGraph.Shared.Identity;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>空间授权相关服务的注册扩展。</summary>
public static class SpaceAuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// 注册空间授权原语（<see cref="ISpaceResolver"/> 的 EF 实现）。
    /// 仅注册服务，不全局启用过滤器——过滤器需通过 <c>[RequireSpaceAccess]</c> 按需激活，
    /// 待 Blazor→Api 身份传播就绪后再挂到具体端点。
    /// </summary>
    public static IServiceCollection AddSpaceAuthorization(this IServiceCollection services)
    {
        services.AddScoped<ISpaceResolver, EfSpaceResolver>();
        return services;
    }
}

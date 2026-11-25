using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using TreeGraph.Shared.Identity;

namespace TreeGraph.Api.Infrastructure;

/// <summary>
/// 按需启用空间授权的属性（opt-in）。挂到控制器或动作上即激活 <see cref="SpaceAuthorizationFilter"/>。
/// 通过 <see cref="NodeRouteKey"/> 指定承载节点 id 的路由键，用于节点级端点的空间解析；
/// 不设置则只校验路由中的 <c>spaceId</c>。
/// 注意：当前 Api 尚未全局要求认证、Blazor 也未向 Api 转发访问令牌，
/// 因此该属性暂不挂到任何端点——待身份传播就绪后再激活。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireSpaceAccessAttribute : Attribute, IFilterFactory
{
    /// <summary>承载节点 id 的路由键（如 <c>nodeId</c>、<c>id</c>）；为空则只校验 <c>spaceId</c>。</summary>
    public string? NodeRouteKey { get; set; }

    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var currentUser = serviceProvider.GetRequiredService<ICurrentUser>();
        var resolver = serviceProvider.GetRequiredService<ISpaceResolver>();
        return new SpaceAuthorizationFilter(currentUser, resolver, NodeRouteKey);
    }
}

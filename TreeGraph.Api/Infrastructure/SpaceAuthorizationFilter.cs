using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TreeGraph.Shared.Identity;

namespace TreeGraph.Api.Infrastructure;

/// <summary>
/// 空间授权过滤器（opt-in，不全局注册）：
/// 路由含 <c>spaceId</c> 时直接比较；否则若指定了 <paramref name="nodeRouteKey"/>，
/// 经 <see cref="ISpaceResolver"/> 解析节点所属空间后比较。
/// app-admin 绕过；解析不到空间时放行（交由端点自身 404 语义处理）。
/// 由 <see cref="RequireSpaceAccessAttribute"/> 按需挂到控制器/动作上激活。
/// </summary>
public sealed class SpaceAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly ICurrentUser _currentUser;
    private readonly ISpaceResolver _spaceResolver;
    private readonly string? _nodeRouteKey;

    public SpaceAuthorizationFilter(ICurrentUser currentUser, ISpaceResolver spaceResolver, string? nodeRouteKey = null)
    {
        _currentUser = currentUser;
        _spaceResolver = spaceResolver;
        _nodeRouteKey = nodeRouteKey;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var routeValues = context.RouteData.Values;

        string? spaceId = routeValues.TryGetValue("spaceId", out var s) ? s?.ToString() : null;

        if (string.IsNullOrWhiteSpace(spaceId)
            && !string.IsNullOrEmpty(_nodeRouteKey)
            && routeValues.TryGetValue(_nodeRouteKey, out var nodeIdObj)
            && nodeIdObj is string nodeId
            && !string.IsNullOrEmpty(nodeId))
        {
            spaceId = await _spaceResolver.ResolveSpaceIdAsync(nodeId, context.HttpContext.RequestAborted);
        }

        // 无空间维度：不阻断（无空间约束的列表端点由其它策略处理，如 app-admin 限制）。
        if (string.IsNullOrWhiteSpace(spaceId))
            return;

        if (!SpaceAccessEvaluator.CanAccess(
                _currentUser.SpaceIds,
                spaceId,
                IdentityRoles.HasAdminRole(_currentUser.Roles)))
        {
            context.Result = new ObjectResult(new { error = "无权访问该空间" }) { StatusCode = 403 };
        }
    }
}

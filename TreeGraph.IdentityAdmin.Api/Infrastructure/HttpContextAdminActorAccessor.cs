using TreeGraph.Shared.Identity;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Api.Infrastructure;

public sealed class HttpContextAdminActorAccessor(IHttpContextAccessor httpContextAccessor) : IAdminActorAccessor
{
    public AdminActor GetRequiredActor()
        => TryGetActor() ?? throw new InvalidOperationException("当前请求未认证，无法执行管理操作");

    public AdminActor? TryGetActor()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var sub = user.FindFirst(IdentityClaimTypes.Subject)?.Value;
        if (string.IsNullOrWhiteSpace(sub))
            return null;

        var username = user.FindFirst(IdentityClaimTypes.PreferredUsername)?.Value
                       ?? user.Identity.Name
                       ?? sub;
        return new AdminActor(sub, username, user.GetRoles());
    }
}

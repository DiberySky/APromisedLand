using System.Security.Claims;
using TreeGraph.Shared.Identity;

namespace TreeGraph.MafWfApi.Infrastructure;

/// <summary>从当前 HTTP 请求的 <see cref="ClaimsPrincipal"/> 读取用户身份。</summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public string UserId => Principal?.FindFirst(IdentityClaimTypes.Subject)?.Value ?? string.Empty;

    public string? UserName => Principal?.FindFirst(IdentityClaimTypes.PreferredUsername)?.Value;

    public IReadOnlyList<string> Roles => Principal?.GetRoles() ?? [];

    public IReadOnlyList<string> SpaceIds => Principal?.GetSpaceIds() ?? [];

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.Ordinal);
}

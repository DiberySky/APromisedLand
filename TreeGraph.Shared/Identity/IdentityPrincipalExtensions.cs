using System.Security.Claims;

namespace TreeGraph.Shared.Identity;

/// <summary>
/// 从 <see cref="ClaimsPrincipal"/> 提取身份信息的公共逻辑。
/// </summary>
public static class IdentityPrincipalExtensions
{
    /// <summary>读取某 claim 的全部取值（去空、去重、保持顺序）。</summary>
    public static IReadOnlyList<string> GetClaimValues(this ClaimsPrincipal principal, string claimType)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return [];

        return principal.FindAll(claimType)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>读取 realm 角色。</summary>
    public static IReadOnlyList<string> GetRoles(this ClaimsPrincipal principal)
        => principal.GetClaimValues(IdentityClaimTypes.Roles);

    /// <summary>读取组（保留完整组名，如 <c>space-123</c>）。</summary>
    public static IReadOnlyList<string> GetGroups(this ClaimsPrincipal principal)
        => principal.GetClaimValues(IdentityClaimTypes.Groups);

    /// <summary>读取空间标识：groups 中带 <c>space-</c> 前缀的项（保留前缀）。</summary>
    public static IReadOnlyList<string> GetSpaceIds(this ClaimsPrincipal principal)
        => principal.GetGroups()
            .Where(g => g.StartsWith(IdentityConstants.SpaceGroupPrefix, StringComparison.Ordinal))
            .ToArray();

    /// <summary>把 <c>space-{id}</c> 组的组名转换为裸空间 ID。</summary>
    public static string? ToSpaceId(this string groupName)
        => groupName.StartsWith(IdentityConstants.SpaceGroupPrefix, StringComparison.Ordinal)
            ? groupName[IdentityConstants.SpaceGroupPrefix.Length..]
            : null;

    /// <summary>由空间 ID 生成空间组名。</summary>
    public static string ToSpaceGroupName(this string spaceId)
        => string.Concat(IdentityConstants.SpaceGroupPrefix, spaceId);
}

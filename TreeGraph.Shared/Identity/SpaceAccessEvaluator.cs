namespace TreeGraph.Shared.Identity;

/// <summary>
/// 空间访问判定的纯逻辑：不依赖 HTTP/数据库，可独立单元测试。
/// 用户的空间标识来自 <see cref="ICurrentUser.SpaceIds"/>（带 <c>space-</c> 前缀），
/// 路由中的 spaceId 是裸 ID（如 <c>1001</c>），二者在此统一归一化比较。
/// </summary>
public static class SpaceAccessEvaluator
{
    /// <summary>
    /// 判断用户是否可访问指定空间。
    /// </summary>
    /// <param name="userSpaceIds">当前用户的空间标识（带 <c>space-</c> 前缀，与 <see cref="ICurrentUser.SpaceIds"/> 同口径）。</param>
    /// <param name="spaceId">目标空间裸 ID（路由值）；为 null/空白表示“无空间约束”，直接放行。</param>
    /// <param name="isAdmin">是否 app-admin；管理员绕过空间校验。</param>
    public static bool CanAccess(IReadOnlyList<string> userSpaceIds, string? spaceId, bool isAdmin)
    {
        if (isAdmin)
            return true;

        if (string.IsNullOrWhiteSpace(spaceId))
            return true;

        var target = spaceId.Trim();
        return userSpaceIds
            .Select(s => s.ToSpaceId())
            .Any(id => string.Equals(id, target, StringComparison.Ordinal));
    }
}

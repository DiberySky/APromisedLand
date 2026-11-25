namespace TreeGraph.Shared.Identity.Admin;

/// <summary>
/// 用户禁用后的撤销标记。命中即拒绝该 sub 的既有 Cookie / 令牌。
/// </summary>
public interface IIdentityRevocationStore
{
    /// <summary>写入撤销标记（TTL 24 小时）。</summary>
    Task MarkRevokedAsync(string subject, DateTimeOffset revokedAt, CancellationToken ct = default);

    /// <summary>移除撤销标记（用户重新启用时调用）。</summary>
    Task ClearRevokedAsync(string subject, CancellationToken ct = default);

    /// <summary>是否已被撤销。</summary>
    Task<bool> IsRevokedAsync(string subject, CancellationToken ct = default);
}

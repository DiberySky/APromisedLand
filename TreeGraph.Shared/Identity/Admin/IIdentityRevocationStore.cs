namespace TreeGraph.Shared.Identity.Admin;

/// <summary>
/// 用户撤销时间戳。禁用、重置密码或全部下线时写入；只拒绝时间不晚于该时间戳的旧令牌。
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

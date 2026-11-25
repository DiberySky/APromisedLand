namespace TreeGraph.IdentityAdmin.Audit;

/// <summary>
/// 审计写入入口（两阶段：pending 意图 + 完成结果）。实现失败时必须抛出，调用方据此 fail-closed。
/// </summary>
public interface IAdminAuditStore
{
    /// <summary>写入 pending 意图记录并返回审计 ID。</summary>
    Task<Guid> BeginAsync(
        string actorSub,
        string? actorName,
        string? serviceClientId,
        string action,
        string? targetType,
        string? targetId,
        string? requestJson,
        string correlationId,
        CancellationToken ct = default);

    /// <summary>更新完成结果。</summary>
    Task CompleteAsync(
        Guid id,
        string result,
        int? keycloakStatus,
        string? error,
        CancellationToken ct = default);

    /// <summary>审计库不可用时的本地文件兜底。</summary>
    Task WriteFallbackAsync(string line, CancellationToken ct = default);
}

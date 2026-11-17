namespace TreeGraph.Contracts.Messaging;

/// <summary>
/// SyncServices 在图侧 DROP TAG 成功后发回核心的幂等清理命令。
/// 核心仅删除仍匹配空间、来源、Tag、版本且已置墓碑的账本行。
/// </summary>
public sealed record TagProjectionCleanupRequested
{
    public required string SpaceId { get; init; }
    public required string SourceAttributeId { get; init; }
    public required string TagName { get; init; }
    public required int Version { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

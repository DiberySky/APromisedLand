namespace TreeGraph.Shared.Messaging;

/// <summary>
/// EAV 实体变更事件（独立实体写操作成功后发布，at-least-once）。
/// 与 InodeChangedEvent 的分工：
///   - 已挂载 inode 的实体：由 InodeChangedEvent 触发 SyncInodeAsync 整体重建
///     （实体文本含宿主 path_view，随宿主一起刷新），不重复发布本事件；
///   - 未挂载的独立实体：EavController 写操作发布本事件，消费端走 SyncEntityAsync。
/// 消息体只带 entityType + entityId，文本与挂载状态由消费侧从 PostgreSQL 现取。
/// </summary>
public record EntityChangedEvent
{
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required InodeChangeKind Kind { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

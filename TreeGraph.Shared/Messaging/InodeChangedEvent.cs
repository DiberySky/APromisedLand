namespace TreeGraph.Shared.Messaging;

/// <summary>
/// iNode 宿主变更事件（Save/Patch/Delete 成功后发布，at-least-once）。
/// 消息体只带 inodeId 与操作类型，文本由消费侧从 PostgreSQL 现取——
/// 天然绕过"发布时 DB 尚未提交"的竞态（PostgreSQL 默认 read-committed，
/// 消费者读到的是最新已提交状态）。
/// </summary>
public record InodeChangedEvent
{
    public required string InodeId { get; init; }
    public required InodeChangeKind Kind { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum InodeChangeKind
{
    Saved,
    Patched,
    Deleted
}

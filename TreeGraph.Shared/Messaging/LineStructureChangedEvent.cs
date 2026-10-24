namespace TreeGraph.Shared.Messaging;

/// <summary>
/// 线路结构变更事件（建结构/替换/清空/加段/删段成功后发布，at-least-once）。
/// PG 内线路段与通用边解耦，本事件只负责驱动图内结构边同步；
/// 消费端只带 LineId 现取 PG 状态（读最新已提交状态），保证幂等可重放。
/// </summary>
public record LineStructureChangedEvent
{
    /// <summary>线路 ID。</summary>
    public required string LineId { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

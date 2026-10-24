namespace TreeGraph.Shared.Messaging;

/// <summary>
/// EdgeInstance 变更事件（Create/Update/Delete 成功后发布，at-least-once）。
/// Deleted 时 PG 行已删，消费端靠 EdgeTypeName/FromNodeId/ToNodeId 载荷定位图边。
/// </summary>
public record EdgeChangedEvent
{
    public required string EdgeId { get; init; }
    public required EdgeChangeKind Kind { get; init; }

    /// <summary>边类型名（edge_instances.type_name，作为图 edge type）。</summary>
    public string? EdgeTypeName { get; init; }

    /// <summary>起点节点 ID。</summary>
    public string? FromNodeId { get; init; }

    /// <summary>终点节点 ID。</summary>
    public string? ToNodeId { get; init; }

    /// <summary>有向/无向（写入图边属性，随事件携带避免消费端回查）。</summary>
    public bool IsDirected { get; init; } = true;

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum EdgeChangeKind
{
    Created,
    Updated,
    Deleted
}

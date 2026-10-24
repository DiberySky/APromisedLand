namespace TreeGraph.Shared.Messaging;

/// <summary>
/// 关系属性值变更事件（relation_attribute_values Insert/Update/Delete 成功后发布，at-least-once）。
/// 事件载荷自带完整端点与图边类型，消费端无需回查 PG（Delete 时 PG 行已删）。
/// 图内边类型 = RelationType，边属性 = value_id / attribute_id / name。
/// </summary>
public record RelationChangedEvent
{
    /// <summary>关系值 Id（也是图边实例 id）。</summary>
    public required string ValueId { get; init; }

    public required string AttributeId { get; init; }

    /// <summary>图边类型（取自属性定义的 relation_type）。</summary>
    public required string RelationType { get; init; }

    /// <summary>起点节点 Id（宿主实体 id）。</summary>
    public required string FromNodeId { get; init; }

    /// <summary>终点节点 Id。</summary>
    public required string ToNodeId { get; init; }

    /// <summary>边实例展示名（可空）。</summary>
    public string? Name { get; init; }

    public required RelationChangeKind Kind { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum RelationChangeKind
{
    Insert,
    Update,
    Delete
}

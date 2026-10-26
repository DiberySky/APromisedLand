namespace TreeGraph.Shared.Messaging;

/// <summary>
/// 边实例变更事件（edges 表 upsert/delete 成功后发布，at-least-once）。
/// 五元组定位图边（SpaceId 路由到目标图空间）；Upsert 时消费端回查 PG 读 props
/// （事件在 SaveChanges 成功后发布），Delete 时 PG 行已删，载荷即定位依据。
/// </summary>
public record EdgeChangedEvent
{
    /// <summary>所属图空间 Id（路由到对应 Nebula space）。</summary>
    public required string SpaceId { get; init; }

    /// <summary>边类型名（图内 edge_type）。</summary>
    public required string EdgeType { get; init; }

    /// <summary>起点节点 Id（src_vid）。</summary>
    public required string SrcVid { get; init; }

    /// <summary>边等级（区分同起终点同类型的多条边）。</summary>
    public required long Rank { get; init; }

    /// <summary>终点节点 Id（dst_vid）。</summary>
    public required string DstVid { get; init; }

    public required EdgeChangeKind Kind { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum EdgeChangeKind
{
    Upsert,
    Delete
}

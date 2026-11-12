namespace TreeGraph.Shared.Messaging;

/// <summary>
/// 空间节点派生 Tag 投影变更事件（NodeEavSky 组合属性元数据写路径发布，at-least-once）。
///
/// 契约（P0-3）：账本（tag_projections）先 Version++ 持久化，再发本事件；
/// 消费者按 <see cref="Version"/> 三路比较 —— msg.Version &lt; 账本当前 → stale 丢弃不执行 DDL；
/// == → 执行 CREATE/ALTER/DROP TAG；&gt; → 按 PG 当前态执行并告警。
/// 删除流程：先置墓碑（IsDeleted + Version++）发事件 → 消费者 DROP TAG IF EXISTS 成功后
/// 条件清墓碑（WHERE IsDeleted AND Version = msg.Version），防同名重建被 stale 事件误清。
/// </summary>
public record SpaceNodeTagProjectionChangedEvent
{
    /// <summary>归属图空间（PG Space.Id）；null = 全空间（逐空间按账本收敛）。</summary>
    public string? SpaceId { get; init; }

    /// <summary>来源属性定义 ID（NodeEavSky AttributeDefinition）。</summary>
    public required string SourceAttributeId { get; init; }

    public required SpaceNodeTagProjectionChangeKind Kind { get; init; }

    /// <summary>账本版本号（发布时已持久化的最新 Version）。</summary>
    public required int Version { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum SpaceNodeTagProjectionChangeKind
{
    Created,
    Altered,
    Deleted
}

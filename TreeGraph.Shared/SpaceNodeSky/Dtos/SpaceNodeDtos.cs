namespace TreeGraph.Shared.SpaceNodeSky.Dtos;

/// <summary>空间节点 DTO（名称/描述读穿 nodes）。</summary>
public record SpaceNodeDto
{
    public required string Id { get; init; }
    public required string SpaceId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>创建空间节点请求。</summary>
public record CreateSpaceNodeRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}

/// <summary>更新空间节点请求（SpaceId 创建后不可改，不在更新面内；跨空间移动 = 删除重建）。</summary>
public record UpdateSpaceNodeRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Tag 投影计划条目（UI「未投影」标记用；账本懒物化，状态由属性定义动态计算）。
/// Status：projected=已投影；tombstoned=墓碑清理中；pending=可投影待建行；unprojected=未投影（见 Reason）。
/// Reason（仅 unprojected）：system_tag / no_columns / no_composite_type / name_conflict。
/// </summary>
public sealed record TagProjectionPlanItemDto(
    string AttributeId, string EntityType, string AttributeName,
    string? TagName, string Status, string? Reason, int ProjectedSpaceCount);

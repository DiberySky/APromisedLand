namespace TreeGraph.Shared.NodeEavSky.Dtos;

/// <summary>
/// 语义搜索单条结果。三端共用（TreeGraph.Api 生产、MafWfApi 转发、Blazor 展示）。
/// HostType 取值：SpaceTree / UnitTree / CategoryTree / KnowledgeTree / TreeNode（未挂载树宿主的裸实体节点）。
/// MatchKind：Node（节点命中）/ Entity（实体命中，EntityType 为实体类型名）。
/// 节点与实体命中独立成条，不合并；节点命中的其原始实体命中已被过滤。
/// </summary>
public sealed record SemanticSearchItemDto(
    string InodeId,
    string HostType,
    string? NodeName,
    string? ParentName,
    string Path,
    string? RootId,
    float Score,
    string MatchKind = "Node",
    string? EntityType = null);

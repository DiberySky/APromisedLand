namespace TreeGraph.Shared.NodeEavSky.Dtos;

/// <summary>
/// 语义搜索单条结果。三端共用（TreeGraph.Api 生产、MafWfApi 转发、Blazor 展示）。
/// HostType 取值：SpaceTree / UnitTree / CategoryTree / TreeNode（未挂载树宿主的裸实体节点）。
/// </summary>
public sealed record SemanticSearchItemDto(
    string InodeId,
    string HostType,
    string? NodeName,
    string? ParentName,
    string Path,
    string? RootId,
    float Score);

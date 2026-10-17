namespace TreeGraph.Shared.NodeEavSky.Dtos;

/// <summary>
/// 语义搜索请求。
/// 三端共用：Blazor → TreeGraphEavApi。
/// </summary>
public sealed record SemanticSearchRequest
{
    public string Query { get; init; } = "";
    public int? TopK { get; init; }
    public string? SpaceId { get; init; }
}

/// <summary>
/// 语义搜索响应。
/// </summary>
public sealed record SemanticSearchResponse(
    string Query,
    int Total,
    IReadOnlyList<SemanticSearchItemDto> Items);

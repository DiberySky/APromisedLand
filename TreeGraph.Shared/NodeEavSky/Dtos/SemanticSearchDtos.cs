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

    /// <summary>相似度绝对下限（0~1）；为空则用服务端 SemanticSearch:MinScore 默认值。</summary>
    public float? MinScore { get; init; }
}

/// <summary>
/// 语义搜索响应。
/// </summary>
public sealed record SemanticSearchResponse(
    string Query,
    int Total,
    IReadOnlyList<SemanticSearchItemDto> Items);

/// <summary>
/// 向量对账结果（POST /api/search/reconcile）。
/// Active/Existing 为对账时的两侧集合规模，Missing/Orphaned 为差集，
/// FixedCount/DeletedCount 为本轮实际修复数。
/// </summary>
public sealed record VectorReconcileResultDto(
    int Active,
    int Existing,
    int Missing,
    int Orphaned,
    int FixedCount,
    int DeletedCount);

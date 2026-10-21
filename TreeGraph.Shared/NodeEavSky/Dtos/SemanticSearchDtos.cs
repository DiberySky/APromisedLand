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

    /// <summary>
    /// hybrid 属性等值过滤（AttributeId → 候选显示值，如 "attr-3"=["110kV","220kV"]）。
    /// 逐项 AND 叠加在向量检索之上；同一属性多值为 OR（MatchAny）语义。
    /// 仅命中 payload attr_{id} 精确匹配的实体点。
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? AttributeFilters { get; init; }

    /// <summary>
    /// hybrid 数值范围过滤（AttributeId → 范围），命中 payload attrn_{id} 数值槽。
    /// 仅 ValueInt/ValueDecimal 类型的属性有数值槽。
    /// </summary>
    public IReadOnlyDictionary<string, AttrRangeFilter>? AttributeRanges { get; init; }
}

/// <summary>数值范围过滤条件（开闭可组合，全空等价不过滤）。</summary>
public sealed record AttrRangeFilter(double? Gte, double? Lte, double? Gt, double? Lt);

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

/// <summary>
/// 向量对账流式进度事件（POST /api/search/reconcile/stream，NDJSON 逐行）。
/// stage=fix：同步节点（InodeId/Name）；stage=purge：清理孤儿（Detail 描述）；
/// stage=summary：结束统计（六个计数字段，与 VectorReconcileResultDto 对应）。
/// </summary>
public sealed record ReconcileStreamEventDto(
    string Stage,
    string InodeId,
    string Name,
    string Detail,
    int? Active = null,
    int? Existing = null,
    int? Missing = null,
    int? Orphaned = null,
    int? FixedCount = null,
    int? DeletedCount = null);

/// <summary>
/// 图按类别同步结果（POST /api/graph-sync/category/{category}，同步刷新对话框展示用）。
/// Vertices/Edges 为该类别参与写入的总量，VerticesWritten/EdgesWritten 为实际成功写入数。
/// </summary>
public sealed record GraphCategorySyncResultDto(
    int Vertices,
    int Edges,
    int VerticesWritten,
    int EdgesWritten,
    IReadOnlyList<string> Errors);

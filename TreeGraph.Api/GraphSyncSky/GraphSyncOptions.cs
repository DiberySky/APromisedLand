namespace TreeGraph.Api.GraphSyncSky;

/// <summary>图同步配置（GraphSync 节）。</summary>
public sealed class GraphSyncOptions
{
    public const string SectionName = "GraphSync";

    public bool Enabled { get; set; } = true;

    /// <summary>TreeGraph.NebulaApi REST 端点（Aspire 注入 NebulaApi:Endpoint 优先）。</summary>
    public string NebulaApiEndpoint { get; set; } = "http://localhost:9393/";

    /// <summary>NebulaGraph 图空间名。</summary>
    public string Space { get; set; } = "treegraph";

    /// <summary>滑动去重窗口（秒）：同一对象在窗口内重复事件合并为一次刷新。</summary>
    public int DedupWindowSeconds { get; set; } = 5;

    /// <summary>全量重建批量写入批大小（≤1000，NebulaApi 上限）。</summary>
    public int RebuildBatchSize { get; set; } = 500;
}

/// <summary>NebulaGraph 图内命名常量。</summary>
public static class GraphNaming
{
    public const string ParentOfEdge = "parent_of";

    public const string SpaceTreeTag = "space_tree";
    public const string UnitTreeTag = "unit_tree";
    public const string CategoryTreeTag = "category_tree";
    public const string KnowledgeTreeTag = "knowledge_tree";
    public const string LineNodeTag = "line_node";

    public static readonly string[] AllNodeTags =
    [
        SpaceTreeTag, UnitTreeTag, CategoryTreeTag, KnowledgeTreeTag, LineNodeTag
    ];
}

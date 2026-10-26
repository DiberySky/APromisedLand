namespace TreeGraph.Api.GraphSyncSky;

/// <summary>图同步配置（GraphSync 节）。</summary>
public sealed class GraphSyncOptions
{
    public const string SectionName = "GraphSync";

    public bool Enabled { get; set; } = true;

    /// <summary>TreeGraph.NebulaFastApi REST 端点（Aspire 注入 NebulaFastApi:Endpoint 优先）。</summary>
    public string NebulaApiEndpoint { get; set; } = "http://localhost:9393/";

    /// <summary>滑动去重窗口（秒）：同一对象在窗口内重复事件合并为一次刷新。</summary>
    public int DedupWindowSeconds { get; set; } = 5;

    /// <summary>全量重建批量写入批大小（≤1000，NebulaApi 上限）。</summary>
    public int RebuildBatchSize { get; set; } = 500;
}

/// <summary>NebulaGraph 图内命名常量。</summary>
public static class GraphNaming
{
    public const string ParentOfEdge = "parent_of";

    /// <summary>线路节点 → 外部 inode（馈入/馈出/联络，属性 role）。</summary>
    public const string ConnectsToEdge = "connects_to";

    /// <summary>
    /// 线路结构段边类型名（LineNode → LineNode）。
    /// 存储 = edges 表（EdgeSky 内置 seed EdgeType，prop: name STRING NULL），rank 恒 0。
    /// </summary>
    public const string LineSegmentEdge = "line_segment";

    /// <summary>
    /// PG 空间 Id → Nebula 图空间名映射（确定性计算，不落库）：
    /// Nebula 标识符不允许 '-'，取 "tg_" + 去横线 GUID（如 tg_7a1e3f5d9b2c4a8e...）。
    /// </summary>
    public static string ToNebulaSpace(string spaceId) => $"tg_{spaceId.Replace("-", "")}";

    public const string SpaceTreeTag = "space_tree";
    public const string UnitTreeTag = "unit_tree";
    public const string CategoryTreeTag = "category_tree";
    public const string KnowledgeTreeTag = "knowledge_tree";
    public const string LineNodeTag = "line_node";

    /// <summary>自由节点（nodes.host_type = "Node"，无宿主扩展表）。</summary>
    public const string NodeTag = "node";

    /// <summary>线路顶点（一条 Line 一个顶点；R7 起无 topology_mode，结构由边连接定义）。</summary>
    public const string LineTag = "line";

    public static readonly string[] AllNodeTags =
    [
        SpaceTreeTag, UnitTreeTag, CategoryTreeTag, KnowledgeTreeTag, LineNodeTag, NodeTag, LineTag
    ];

    /// <summary>按 tag 声明属性列（schema 初始化与 ALTER 补列共用）。</summary>
    public static readonly IReadOnlyDictionary<string, string[]> TagColumns =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [SpaceTreeTag] = ["name", "node_type", "is_archived"],
            [UnitTreeTag] = ["name", "node_type", "is_archived"],
            [CategoryTreeTag] = ["name", "node_type", "is_archived"],
            [KnowledgeTreeTag] = ["name", "node_type", "is_archived"],
            [LineNodeTag] =
            [
                "name", "node_type", "is_archived",
                "line_id", "line_name", "terminal_role", "terminal_operating_state"
            ],
            [NodeTag] = ["name", "node_type", "is_archived"],
            [LineTag] = ["name", "node_type", "is_archived"]
        };
}

/// <summary>内置种子空间 Id（与 Space.SeedData 对齐）：树 → 图空间路由锚点。</summary>
internal static class SeedSpaceIds
{
    /// <summary>「类型」空间：分类树（CategoryTree）路由目标。</summary>
    public const string Type = "8b2f4a6e-0c3d-5b9f-2a1e-4d6c8f0b2d3e";

    /// <summary>「单位」空间：单位树（UnitTree）路由目标。</summary>
    public const string Unit = "9c3a5b7f-1d4e-6c0a-3b2f-5e7d9a1c3e4f";

    /// <summary>「知识库」空间：知识树（KnowledgeTree）路由目标。</summary>
    public const string Knowledge = "ad5e6c8f-2b3d-4a7e-9c0f-1d2b3e4f5a6b";
}

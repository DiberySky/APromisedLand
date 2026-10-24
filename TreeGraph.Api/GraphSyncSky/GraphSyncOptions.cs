namespace TreeGraph.Api.GraphSyncSky;

/// <summary>图同步配置（GraphSync 节）。</summary>
public sealed class GraphSyncOptions
{
    public const string SectionName = "GraphSync";

    public bool Enabled { get; set; } = true;

    /// <summary>TreeGraph.NebulaFastApi REST 端点（Aspire 注入 NebulaFastApi:Endpoint 优先）。</summary>
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

    /// <summary>线路节点 → 外部 inode（馈入/馈出/联络，属性 role）。</summary>
    public const string ConnectsToEdge = "connects_to";

    /// <summary>
    /// 线路结构段边类型名（LineNode → LineNode）。
    /// R7 起存储 = relation_attribute_values（seed 属性 LineStructure.next），
    /// 图边 schema 经 R6 relation 管道动态创建（列 value_id/attribute_id/name），此处仅留类型名常量。
    /// </summary>
    public const string LineSegmentEdge = "line_segment";

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

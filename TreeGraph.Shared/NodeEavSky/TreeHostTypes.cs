namespace TreeGraph.Shared.NodeEavSky;

/// <summary>
/// 树节点宿主类型集合（四棵树 + EAV 树；不含通用节点 Node）。
/// 前后端共用：API 用于创建属性时拦截，前端用于类型下拉过滤。
/// 树节点禁止挂载 property_tree 属性——避免「树节点的属性又是一棵树」套娃。
/// </summary>
public static class TreeHostTypes
{
    /// <summary>树节点宿主 entityType 名（与 nodes.host_type 一致）。</summary>
    public static readonly HashSet<string> All = new(System.StringComparer.Ordinal)
    {
        "SpaceTree", "UnitTree", "CategoryTree", "KnowledgeTree", "EavTree"
    };

    /// <summary>是否为树节点宿主。</summary>
    public static bool IsTreeHost(string entityType) => All.Contains(entityType);
}

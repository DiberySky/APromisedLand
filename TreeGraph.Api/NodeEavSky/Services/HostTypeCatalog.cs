namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>代码内置的 Inode 宿主类型（不落库，无需在实体类型目录登记）。</summary>
public sealed record HostType(
    string EntityType,
    string DisplayName,
    string Description,
    int DisplayOrder);

/// <summary>
/// 宿主类型清单。
///
/// 四棵树（SpaceTree/UnitTree/CategoryTree/KnowledgeTree）与通用节点（Node）的类型名由各宿主服务
/// 硬编码写入 nodes.host_type。创建属性时这些类型无需在 entity_type_catalog 登记即可使用；
/// 列表端点会把它们并入候选，保证属性管理页能选到宿主类型。其余用途仅为显示名映射。
/// </summary>
public static class HostTypeCatalog
{
    public static IReadOnlyList<HostType> All { get; } =
    [
        new("SpaceTree", "空间树", "内置 Inode 宿主：空间树节点（属性挂载用）", 0),
        new("UnitTree", "单位树", "内置 Inode 宿主：单位树节点（属性挂载用）", 1),
        new("CategoryTree", "分类树", "内置 Inode 宿主：分类树节点（属性挂载用）", 2),
        new("KnowledgeTree", "知识树", "内置 Inode 宿主：知识树节点（属性挂载用）", 3),
        new("Node", "通用节点", "内置 Inode 宿主：无归属通用节点（属性挂载用）", 4)
    ];

    private static readonly Dictionary<string, HostType> ByName =
        All.ToDictionary(t => t.EntityType, StringComparer.Ordinal);

    /// <summary>是否为内置宿主类型（宿主类型不要求目录登记）。</summary>
    public static bool Contains(string entityType) => ByName.ContainsKey(entityType);

    /// <summary>按类型名取内置宿主类型；非宿主类型返回 null。</summary>
    public static HostType? Find(string entityType)
        => ByName.GetValueOrDefault(entityType);

    /// <summary>类型显示名：目录优先（调用方传入已查到的），否则回退宿主类型显示名，再回退类型名。</summary>
    public static string DisplayNameOrSelf(string entityType, string? catalogDisplayName)
        => catalogDisplayName
           ?? Find(entityType)?.DisplayName
           ?? entityType;
}

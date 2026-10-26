namespace TreeGraph.Api.NodeEavSky.Entities;

/// <summary>
/// 实体类型目录（独立于属性定义）。
/// 创建属性前，其 EntityType 必须已存在于本表。
/// </summary>
public class EntityTypeDefinition
{
    public string EntityTypeId { get; set; } = "";
    public string EntityType { get; set; } = "";         // "UnitTree"
    public string DisplayName { get; set; } = "";        // "单位树"
    public string? Description { get; set; }             // 说明
    /// <summary>
    /// 所属空间 Id（spaces.id）。null = 共享类型（所有空间可见）；
    /// 非 null = 该空间独占类型（仅所属空间上下文可见）。
    /// 命名仍全局唯一（uq_entity_type），独占仅是可见性隔离。
    /// </summary>
    public string? OwnerSpaceId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>内置实体类型种子（固定 id，拒改拒删）。当前无内置种子。</summary>
    public static List<EntityTypeDefinition> SeedData() => [];

    /// <summary>内置种子 id 集合（固定 GUID，拒改拒删）。</summary>
    private static readonly HashSet<string> BuiltinIds =
        SeedData().Select(s => s.EntityTypeId).ToHashSet(StringComparer.Ordinal);

    /// <summary>是否内置种子实体类型（内置保护：元数据端点拒改拒删）。</summary>
    public static bool IsBuiltin(string entityTypeId) => BuiltinIds.Contains(entityTypeId);
}

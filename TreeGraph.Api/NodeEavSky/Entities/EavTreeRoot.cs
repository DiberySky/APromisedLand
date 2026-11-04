namespace TreeGraph.Api.NodeEavSky.Entities;

/// <summary>
/// property_tree 映射表：(EAV 实体, 属性) → 专属 eav_trees rootNode。
///
/// 语义：
///   - Id 即 rootNode.Id，与 nodes / eav_trees 共享主键（一树一根，全局唯一）
///   - 属性树的值不落 attribute_values（容器族原则：composite/table/property_tree 均走专属存储通道）
///   - rootNode.Name 唯一真源在 nodes（R2 Name 收编），本表不冗余 name
///
/// 约束：
///   - UNIQUE (EntityId, AttributeId)：一个实体的一个属性树属性至多挂载一棵根
///   - BindingId 为冗余便利列（绑定 ⇌ 实体 1:1），非绑定宿主（无绑定行的 EntityId）为 null；
///     FK → inode_entity 用 Restrict：删除绑定须先经 DeleteEntityAsync 守卫清理，防绕过
/// </summary>
public class EavTreeRoot
{
    /// <summary>映射行主键，即 rootNode.Id（GUID 字符串）</summary>
    public string Id { get; set; } = "";

    /// <summary>EAV 实体 ID（绑定私有的 entity_id）</summary>
    public string EntityId { get; set; } = "";

    /// <summary>属性树属性定义 ID（attribute_catalog.attribute_id）</summary>
    public string AttributeId { get; set; } = "";

    /// <summary>所属绑定（冗余列，可空；级联清理与排障用）</summary>
    public string? BindingId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public InodeEntity? Binding { get; set; }
}

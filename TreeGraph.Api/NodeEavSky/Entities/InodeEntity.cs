namespace TreeGraph.Api.NodeEavSky.Entities;

/// <summary>
/// iNode ↔ 实体归属关系。
///
/// 约束：
///   - PK (InodeId, EntityType)：每个 iNode 每个类型只有 1 个实体
///   - UNIQUE (EntityType, EntityId)：每个实体只属于 1 个 iNode
///
/// EntityId 直接复用现有 EAV 的 entity_id
/// （即 attribute_values.entity_id 指向的值）。
/// </summary>
public class InodeEntity
{
    /// <summary>iNode ID（GUID 字符串）</summary>
    public string InodeId { get; set; } = "";

    /// <summary>实体类型名（如 "Product"）</summary>
    public string EntityType { get; set; } = "";

    /// <summary>实体 ID（GUID 字符串）</summary>
    public string EntityId { get; set; } = "";

    public DateTimeOffset AttachedAt { get; set; } = DateTimeOffset.UtcNow;
}

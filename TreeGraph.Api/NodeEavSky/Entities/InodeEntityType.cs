namespace TreeGraph.Api.NodeEavSky.Entities;

/// <summary>
/// iNode → EntityType 的声明关联（N:N）。
///
/// 语义：某个 iNode 声明"我会使用这个实体类型"。
/// UI 上表现为"该 iNode 详情页显示哪些类型卡片"。
///
/// 用类型名（如 "Product"）而不是 typeId，与现有 EAV 的
/// attribute_catalog.entity_type 字段保持一致。
///
/// 无 inode 表（外部应用维护 iNode），inode_id 只是字符串。
/// </summary>
public class InodeEntityType
{
    /// <summary>iNode ID（GUID 字符串）</summary>
    public string InodeId { get; set; } = "";

    /// <summary>实体类型名（如 "Product"）</summary>
    public string EntityType { get; set; } = "";

    public DateTimeOffset AttachedAt { get; set; } = DateTimeOffset.UtcNow;
}

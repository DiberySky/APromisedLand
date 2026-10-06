namespace TreeGraph.StringTree.Contracts;

/// <summary>
/// 空间：StringTree 的根节点。
///
/// 数据上等同 ParentId == null 的 StringNode；
/// 独立 DTO 用于语义区分与未来扩展。
/// </summary>
public class SpaceDto
{
    /// <summary>空间 Id（GUID 字符串，36 字符）。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>空间名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>空间描述（可空）。</summary>
    public string? Description { get; set; }

    /// <summary>
    /// 本空间使用的 EAV EntityType。
    ///   - "StringTreeNode"                 → 共享默认属性集
    ///   - "StringTreeNode:{guid}"          → 独立属性集
    ///   - "" 或 "auto"（仅创建时）          → 由后端生成独立 EntityType
    /// </summary>
    public string EntityType { get; set; } = string.Empty;
}

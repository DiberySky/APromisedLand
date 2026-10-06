namespace TreeGraph.StringTree.Contracts;

public class SpaceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// 本空间使用的 EAV EntityType。
    ///   - "StringTreeNode"           → 共享默认属性集
    ///   - "StringTreeNode:{guid}"    → 独立属性集
    ///   - "" 或 "auto"（仅创建时）   → 由后端生成独立 EntityType
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// 仅创建时生效：从指定 EntityType 复制属性定义。
    /// null/空 表示不复制（使用空属性集）。
    /// </summary>
    public string? TemplateEntityType { get; set; }
}

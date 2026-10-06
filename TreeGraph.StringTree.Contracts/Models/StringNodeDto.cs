namespace TreeGraph.StringTree.Contracts;

public class StringNodeDto
{
    /// <summary>节点 Id（GUID 字符串，36 字符）；同时直接作为 EAV EntityId。</summary>
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool HasChildren { get; set; }
    public string? Description { get; set; }
}

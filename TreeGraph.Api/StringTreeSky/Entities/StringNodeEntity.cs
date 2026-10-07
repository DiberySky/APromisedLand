using TreeGraph.Shared.StringTreeSky.Contracts;

namespace TreeGraph.Api.StringTreeSky.Entities;

public class StringNodeEntity
{
    /// <summary>主键（GUID 字符串，36 字符）；同时直接作为 EAV EntityId。</summary>
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public int SortOrder { get; set; }
    public string? Description { get; set; }

    /// <summary>EAV EntityType（子节点继承父节点）。</summary>
    public string EntityType { get; set; } = StringTreeEntityTypes.Node;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public StringNodeEntity? Parent { get; set; }
    public List<StringNodeEntity> Children { get; set; } = new();
}

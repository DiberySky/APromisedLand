namespace TreeGraph.Api.StringTreeSky.Entities;

public class StringNodeEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public StringNodeEntity? Parent { get; set; }
    public List<StringNodeEntity> Children { get; set; } = new();
}

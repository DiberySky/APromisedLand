namespace TreeGraph.StringTree.Contracts;

public class StringNodeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool HasChildren { get; set; }
    public string? Description { get; set; }
}

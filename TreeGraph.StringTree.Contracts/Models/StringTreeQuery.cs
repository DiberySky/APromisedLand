namespace TreeGraph.StringTree.Contracts;

public class StringTreeQuery
{
    public int? ParentId { get; set; }
    public string? Keyword { get; set; }
    public bool IncludeArchived { get; set; }
}

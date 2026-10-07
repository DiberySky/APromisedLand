namespace TreeGraph.Shared.StringTreeSky.Contracts;

public class StringTreeQuery
{
    public string? ParentId { get; set; }
    public string? Keyword { get; set; }
    public bool IncludeArchived { get; set; }
}

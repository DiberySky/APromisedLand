namespace MAFRagService.Models;

public class GraphContext
{
    public List<string> PathNodes { get; set; } = new();
    public List<GraphRelation> Relations { get; set; } = new();
}

public class GraphRelation
{
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

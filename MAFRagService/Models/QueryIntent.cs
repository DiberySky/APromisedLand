namespace MAFRagService.Models;

public class QueryIntent
{
    public string OriginalQuestion { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public List<string> Entities { get; set; } = new();
    public string Intent { get; set; } = "Query"; // Query, Compare, Explain
    public string Tenant { get; set; } = "default";
}

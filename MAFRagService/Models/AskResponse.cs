namespace MAFRagService.Models;

public class AskResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<Source> Sources { get; set; } = new();
    public string? Tenant { get; set; }
}

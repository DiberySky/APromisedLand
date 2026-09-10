namespace MAFRagService.Models;

public class AskRequest
{
    public string Question { get; set; } = string.Empty;
    public string Tenant { get; set; } = "default";
    public string? Version { get; set; }
    public bool? UseGraph { get; set; } = true;
    public int? TopK { get; set; } = 5;
}

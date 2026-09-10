namespace MAFRagService.Models;

public class EntityInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Tenant { get; set; } = "default";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

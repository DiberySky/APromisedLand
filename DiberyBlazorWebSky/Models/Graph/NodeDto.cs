using System.Text.Json.Serialization;

namespace DiberyBlazorWebSky.Models.Graph;

public record NodeDto
{
    [JsonPropertyName("GUID")]
    public Guid Guid { get; set; }
    
    [JsonPropertyName("GraphGUID")]
    public Guid GraphGuid { get; set; }
    
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("Labels")]
    public List<string>? Labels { get; set; }
    
    public Guid TenantGuid { get; init; }
    public Dictionary<string, string?>? Tags { get; init; }
    public Dictionary<string, object?>? Data { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime LastUpdateUtc { get; init; }
}
using System.Text.Json.Serialization;

namespace DiberyBlazorWebSky.Models.Graph;

public record EdgeDto
{

    [JsonPropertyName("GUID")]
    public Guid Guid { get; set; }
    
    public Guid TenantGuid { get; init; }

    [JsonPropertyName("GraphGUID")]
    public Guid GraphGuid { get; set; }
    public Guid From { get; init; }
    public Guid To { get; init; }

    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    public int Cost { get; init; }

    [JsonPropertyName("Labels")]
    public List<string>? Labels { get; set; }

    public Dictionary<string, string?>? Tags { get; init; }
    public DateTime CreatedUtc { get; init; }
}
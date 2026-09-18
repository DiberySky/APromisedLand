namespace DiberyBlazorWebSky.Models.Graph;

public record NodeDto
{
    public Guid Guid { get; init; }
    public Guid TenantGuid { get; init; }
    public Guid GraphGuid { get; init; }
    public string Name { get; init; } = string.Empty;
    public List<string>? Labels { get; init; }
    public Dictionary<string, string?>? Tags { get; init; }
    public Dictionary<string, object?>? Data { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime LastUpdateUtc { get; init; }
}
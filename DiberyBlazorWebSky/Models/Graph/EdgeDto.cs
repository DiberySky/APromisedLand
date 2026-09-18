namespace DiberyBlazorWebSky.Models.Graph;

public record EdgeDto
{
    public Guid Guid { get; init; }
    public Guid TenantGuid { get; init; }
    public Guid GraphGuid { get; init; }
    public Guid From { get; init; }
    public Guid To { get; init; }
    public string Name { get; init; } = string.Empty;    // ★ 边的类型名
    public int Cost { get; init; }
    public List<string>? Labels { get; init; }
    public Dictionary<string, string?>? Tags { get; init; }
    public DateTime CreatedUtc { get; init; }
}
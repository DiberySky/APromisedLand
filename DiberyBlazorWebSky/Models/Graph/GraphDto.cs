namespace DiberyBlazorWebSky.Models.Graph;

public record GraphDto
{
    public Guid Guid { get; init; }
    public Guid TenantGuid { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
    public DateTime LastUpdateUtc { get; init; }
}
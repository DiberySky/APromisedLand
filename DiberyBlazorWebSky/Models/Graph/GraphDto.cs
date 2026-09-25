namespace DiberyBlazorWebSky.Models.Graph;

public record GraphDto
{
    public Guid Guid { get; init; }
    public Guid TenantGuid { get; init; }
    public string Name { get; set; } = string.Empty;  // 修复：init → set，支持重命名
    public DateTime CreatedUtc { get; init; }
    public DateTime LastUpdateUtc { get; init; }
}
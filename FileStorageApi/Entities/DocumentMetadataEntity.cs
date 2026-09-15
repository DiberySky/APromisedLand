// Entities/DocumentMetadataEntity.cs
namespace FileStorageApi.Entities;

public class DocumentMetadataEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DocId { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string Tenant { get; set; } = "default";
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long Size { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public string? Sha256 { get; set; }
    public string Status { get; set; } = "active";
    public string? TagsJson { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
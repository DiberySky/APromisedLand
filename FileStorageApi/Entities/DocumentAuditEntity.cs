// Entities/DocumentAuditEntity.cs
namespace FileStorageApi.Entities;

public class DocumentAuditEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DocId { get; set; } = string.Empty;
    public string Tenant { get; set; } = "default";
    public string Action { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public string? DetailsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
// Entities/IndexTaskEntity.cs
namespace FileStorageApi.Entities;

public class IndexTaskEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DocId { get; set; } = string.Empty;
    public string Tenant { get; set; } = "default";
    public string Status { get; set; } = "pending";
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
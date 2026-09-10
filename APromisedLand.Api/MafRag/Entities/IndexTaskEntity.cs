using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APromisedLand.Api.MafRag.Entities;

[Table("index_tasks")]
public class IndexTaskEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(128)]
    public string DocId { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string Tenant { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string TaskType { get; set; } = string.Empty; // embedding, graph, entity

    [MaxLength(32)]
    public string Status { get; set; } = "pending";

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

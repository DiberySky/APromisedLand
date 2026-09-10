using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APromisedLand.Api.MafRag.Entities;

[Table("domain_events")]
public class DomainEventEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(128)]
    public string StreamId { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string EventType { get; set; } = string.Empty;

    [Required, Column(TypeName = "jsonb")]
    public string EventData { get; set; } = string.Empty;

    public int Version { get; set; }

    [MaxLength(64)]
    public string? Tenant { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

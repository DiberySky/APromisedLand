using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APromisedLand.Api.MafRag.Entities;

[Table("doc_audit")]
public class DocumentAuditEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(128)]
    public string DocId { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string Tenant { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? OldVersion { get; set; }

    [MaxLength(32)]
    public string? NewVersion { get; set; }

    [MaxLength(128)]
    public string? Operator { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(DocId))]
    public DocumentMetadataEntity? Document { get; set; }
}

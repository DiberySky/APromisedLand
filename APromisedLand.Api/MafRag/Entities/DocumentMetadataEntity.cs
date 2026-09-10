using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APromisedLand.Api.MafRag.Entities;

[Table("doc_metadata")]
public class DocumentMetadataEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(128)]
    public string DocId { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string Tenant { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string Version { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string FileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [MaxLength(128)]
    public string? MimeType { get; set; }

    [Required]
    public string BlobUri { get; set; } = string.Empty;

    [Required, MaxLength(512)]
    public string BlobName { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string? ExtraMetadata { get; set; } // JSON

    [MaxLength(32)]
    public string Status { get; set; } = "active";

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DocumentAuditEntity> Audits { get; set; } = new List<DocumentAuditEntity>();
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace FileTransService.Models;

public class FileMetadata
{
    public Guid Id { get; set; }
    [MaxLength(100)]
    public string Fid { get; set; } = string.Empty;
    [MaxLength(100)]
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    [MaxLength(100)]
    public string MimeType { get; set; } = string.Empty;
    [MaxLength(1000)]
    public string? FileHash { get; set; }
    [MaxLength(100)]
    public string? BusinessId { get; set; }
    [MaxLength(100)]
    public string? BusinessType { get; set; }
    [MaxLength(100)]
    public string? UploaderId { get; set; }
    public DateTimeOffset UploadTime { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastAccessTime { get; set; }
    public long DownloadCount { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    [MaxLength(5000)]
    public string? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
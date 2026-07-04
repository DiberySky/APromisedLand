namespace FileTransService.Models;

public class UploadFileRequest
{
    public Stream FileStream { get; set; } = Stream.Null;
    public string FileName { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long? FileSize { get; set; }
    public string? BusinessId { get; set; }
    public string? BusinessType { get; set; }
    public string? UploaderId { get; set; }
    public Dictionary<string, object>? CustomMetadata { get; set; }
}
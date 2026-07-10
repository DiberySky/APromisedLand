namespace APromisedLand.Api.Projects.SeaweedFS.Models;

public class CompleteUploadRequest
{
    public string UploadId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public string? BusinessId { get; set; }
}
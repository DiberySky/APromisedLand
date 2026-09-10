namespace MAFRagService.Models;

public class DocumentMetadata
{
    public string DocId { get; set; } = string.Empty;
    public string Tenant { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public string BlobUri { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraMetadata { get; set; } = new();
}

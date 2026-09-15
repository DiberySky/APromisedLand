namespace DiberyBlazorWebSky.Models;

/// <summary>与 FileStorageApi.Uploads.CompleteUploadResponse 对应。</summary>
public record CompleteUploadResponse
{
    public Guid UploadId { get; init; }
    public string DocId { get; init; } = string.Empty;
    public int Version { get; init; }
    public string ObjectKey { get; init; } = string.Empty;
    public long Size { get; init; }
    public string? Sha256 { get; init; }
}
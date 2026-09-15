namespace DiberyBlazorWebSky.Models;

/// <summary>与 FileStorageApi.Uploads.CompleteUploadRequest 对应。</summary>
public record CompleteUploadRequest
{
    public string? DocId { get; init; }
    public string? TagsJson { get; init; }
    public string? MetadataJson { get; init; }
    public string? Sha256 { get; init; }
}
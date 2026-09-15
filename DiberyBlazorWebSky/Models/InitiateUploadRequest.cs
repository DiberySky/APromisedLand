namespace DiberyBlazorWebSky.Models;

/// <summary>与 FileStorageApi.Uploads.InitiateUploadRequest 对应。</summary>
public record InitiateUploadRequest
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public long TotalSize { get; init; }
    public int? ChunkSize { get; init; }
    public string? DocId { get; init; }
    public string? Fingerprint { get; init; }
}
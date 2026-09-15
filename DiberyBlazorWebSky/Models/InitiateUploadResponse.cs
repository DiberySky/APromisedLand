namespace DiberyBlazorWebSky.Models;

/// <summary>与 FileStorageApi.Uploads.InitiateUploadResponse 对应。</summary>
public record InitiateUploadResponse
{
    public Guid UploadId { get; init; }
    public int ChunkSize { get; init; }
    public int TotalChunks { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public bool Resumed { get; init; }
}
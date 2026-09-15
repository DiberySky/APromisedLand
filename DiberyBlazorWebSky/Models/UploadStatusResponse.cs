namespace DiberyBlazorWebSky.Models;

/// <summary>
/// 与 FileStorageApi.Uploads.UploadStatusResponse 对应。
/// 注意：<see cref="Status"/> 可能出现 "cleaned"（清理器已回收分块）。
/// </summary>
public record UploadStatusResponse
{
    public Guid UploadId { get; init; }
    public string Status { get; init; } = "pending";
    public long TotalSize { get; init; }
    public int ChunkSize { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string? Fingerprint { get; init; }
    public int TotalChunks { get; init; }
    public int ReceivedCount { get; init; }
    public IReadOnlyList<int> ReceivedChunks { get; init; } = [];
    public IReadOnlyList<int> MissingChunks { get; init; } = [];
    public DateTimeOffset ExpiresAt { get; init; }
}
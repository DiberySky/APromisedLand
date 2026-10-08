namespace TreeGraph.Shared.FileStorageSky.Contracts;

/// <summary>
/// 上传会话状态响应。
/// 注意：原 FromSession 工厂方法已移至 UploadResponseFactory（依赖 UploadSessionEntity）。
/// </summary>
public sealed class UploadStatusResponse
{
    public Guid UploadId { get; init; }
    public string Status { get; init; } = "pending";

    public long TotalSize { get; init; }
    public int ChunkSize { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string? Fingerprint { get; init; }

    public int TotalChunks { get; init; }
    public int ReceivedCount { get; init; }
    public IReadOnlyList<int> ReceivedChunks { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> MissingChunks { get; init; } = Array.Empty<int>();
    public DateTimeOffset ExpiresAt { get; init; }

    public UploadStatusResponse() { }

    public UploadStatusResponse(
        Guid uploadId, string status,
        long totalSize, int chunkSize, string fileName, string? fingerprint,
        int totalChunks, int receivedCount,
        IReadOnlyList<int> receivedChunks, IReadOnlyList<int> missingChunks,
        DateTimeOffset expiresAt)
    {
        UploadId       = uploadId;
        Status         = status;
        TotalSize      = totalSize;
        ChunkSize      = chunkSize;
        FileName       = fileName;
        Fingerprint    = fingerprint;
        TotalChunks    = totalChunks;
        ReceivedCount  = receivedCount;
        ReceivedChunks = receivedChunks;
        MissingChunks  = missingChunks;
        ExpiresAt      = expiresAt;
    }

    public double Progress =>
        TotalChunks <= 0 ? 0d : (double)ReceivedCount / TotalChunks;

    public bool IsComplete => ReceivedCount >= TotalChunks;
}

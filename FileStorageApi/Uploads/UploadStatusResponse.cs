namespace FileStorageApi.Uploads;

public sealed class UploadStatusResponse
{
    public Guid UploadId { get; init; }
    public string Status { get; init; } = "pending";

    /// <summary>★ P2-3：客户端校验本地文件是否与会话一致</summary>
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

    public static UploadStatusResponse FromSession(
        Entities.UploadSessionEntity session,
        IReadOnlyList<int> receivedChunks)
    {
        var received = receivedChunks.Distinct().OrderBy(i => i).ToList();
        var receivedSet = new HashSet<int>(received);
        var missing = Enumerable.Range(0, session.TotalChunks)
            .Where(i => !receivedSet.Contains(i))
            .ToList();

        return new UploadStatusResponse(
            session.Id, session.Status,
            session.TotalSize, session.ChunkSize,
            session.FileName, session.Fingerprint,
            session.TotalChunks, received.Count,
            received, missing, session.ExpiresAt);
    }

    public double Progress =>
        TotalChunks <= 0 ? 0d : (double)ReceivedCount / TotalChunks;

    public bool IsComplete => ReceivedCount >= TotalChunks;
}
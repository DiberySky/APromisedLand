using FileStorageApi.Entities;

namespace FileStorageApi.Uploads;

public sealed class InitiateUploadResponse
{
    public Guid UploadId { get; init; }
    public int ChunkSize { get; init; }
    public int TotalChunks { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>★ 是否复用了既有会话（Fingerprint 命中）。</summary>
    public bool Resumed { get; init; }

    public InitiateUploadResponse() { }

    public InitiateUploadResponse(
        Guid uploadId, int chunkSize, int totalChunks,
        DateTimeOffset expiresAt, bool resumed = false)
    {
        UploadId    = uploadId;
        ChunkSize   = chunkSize;
        TotalChunks = totalChunks;
        ExpiresAt   = expiresAt;
        Resumed     = resumed;
    }

    public static InitiateUploadResponse FromSession(
        UploadSessionEntity session, bool resumed = false)
        => new(session.Id, session.ChunkSize, session.TotalChunks,
            session.ExpiresAt, resumed);
}
namespace TreeGraph.Shared.FileStorageSky.Contracts;

/// <summary>
/// 发起上传的响应。
/// 注意：原 FileStorageApi 中的 FromSession 工厂方法已移至
/// TreeGraph.FileStorageApi.Uploads.UploadResponseFactory（依赖 UploadSessionEntity）。
/// </summary>
public sealed class InitiateUploadResponse
{
    public Guid UploadId { get; init; }
    public int ChunkSize { get; init; }
    public int TotalChunks { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>是否复用了既有会话（Fingerprint 命中）。</summary>
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
}

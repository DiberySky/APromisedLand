namespace FileStorageApi.Uploads;

/// <summary>上传单个分块的响应体（class 版本）。</summary>
public sealed class UploadChunkResponse
{
    /// <summary>上传会话 ID。</summary>
    public Guid UploadId { get; init; }

    /// <summary>本次成功写入的分块索引。</summary>
    public int ChunkIndex { get; init; }

    /// <summary>服务端当前已接收的不同分块数量。</summary>
    public int ReceivedCount { get; init; }

    /// <summary>会话总分块数。</summary>
    public int TotalChunks { get; init; }

    /// <summary>无参构造，供序列化器使用。</summary>
    public UploadChunkResponse() { }

    /// <summary>便利构造。</summary>
    public UploadChunkResponse(
        Guid uploadId, int chunkIndex, int receivedCount, int totalChunks)
    {
        UploadId      = uploadId;
        ChunkIndex    = chunkIndex;
        ReceivedCount = receivedCount;
        TotalChunks   = totalChunks;
    }

    /// <summary>当前接收进度（0.0 ~ 1.0）。</summary>
    public double Progress =>
        TotalChunks <= 0 ? 0d : (double)ReceivedCount / TotalChunks;

    /// <summary>是否已收齐全部分块。</summary>
    public bool IsComplete => ReceivedCount >= TotalChunks;
}
namespace MAFRagService.Services.Weaviate;

public sealed class ChunkRecord
{
    public string DocId { get; init; } = string.Empty;
    public string Tenant { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public int ChunkIndex { get; init; }
    public string Content { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public float[] Vector { get; init; } = Array.Empty<float>();
}

public interface IWeaviateChunkWriter
{
    /// <summary>按 docId + version + tenant 删除旧 chunk，保证重试幂等。</summary>
    Task DeleteByDocAsync(string docId, string version, string tenant, CancellationToken ct);

    /// <summary>批量写入（含向量）。</summary>
    Task UpsertBatchAsync(IReadOnlyList<ChunkRecord> records, CancellationToken ct);
}
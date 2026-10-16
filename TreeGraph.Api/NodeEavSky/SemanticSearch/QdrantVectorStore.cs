using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>
/// Qdrant 向量存储薄封装。
/// 连接复用 Qdrant.Client（gRPC），PointId = inodeId（UUID）。
/// </summary>
public sealed class QdrantVectorStore
{
    private readonly QdrantClient _client;
    private readonly SemanticSearchOptions _options;
    private readonly ILogger<QdrantVectorStore> _logger;

    public QdrantVectorStore(
        QdrantClient client,
        IOptions<SemanticSearchOptions> options,
        ILogger<QdrantVectorStore> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task UpsertAsync(
        string inodeId, float[] vector,
        string hostType, string spaceId, string sourceHash, string sourceText,
        CancellationToken ct = default)
    {
        var point = new PointStruct
        {
            Id = new PointId { Uuid = inodeId },
            Vectors = vector,
            Payload =
            {
                ["host_type"] = hostType,
                ["space_id"] = spaceId,
                ["source_hash"] = sourceHash,
                ["source_text"] = sourceText.Length > 4000
                    ? sourceText[..4000]
                    : sourceText
            }
        };

        await _client.UpsertAsync(_options.CollectionName, [point],
            cancellationToken: ct);

        _logger.LogDebug("Qdrant upsert：inode={InodeId} hash={Hash}",
            inodeId, sourceHash[..8]);
    }

    public async Task DeleteAsync(string inodeId, CancellationToken ct = default)
    {
        // Qdrant.Client 提供 (collection, Guid) 重载；inodeId 为 GUID 字符串
        await _client.DeleteAsync(_options.CollectionName,
            Guid.Parse(inodeId),
            cancellationToken: ct);
        _logger.LogDebug("Qdrant delete：inode={InodeId}", inodeId);
    }

    public async Task<IReadOnlyList<SemanticHit>> SearchAsync(
        float[] queryVector, int topK, string? spaceId,
        CancellationToken ct = default)
    {
        Filter? filter = null;
        if (!string.IsNullOrWhiteSpace(spaceId))
        {
            filter = new Filter();
            filter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "space_id",
                    Match = new Match { Keyword = spaceId }
                }
            });
        }

        var results = await _client.SearchAsync(
            _options.CollectionName,
            queryVector,
            filter: filter,
            limit: (ulong)topK,
            payloadSelector: true,
            cancellationToken: ct);

        return results.Select(r => new SemanticHit(
            InodeId: r.Id.Uuid,
            Score: r.Score,
            HostType: r.Payload.TryGetValue("host_type", out var h)
                ? h.StringValue : "",
            SpaceId: r.Payload.TryGetValue("space_id", out var s)
                ? s.StringValue : "",
            SourceHash: r.Payload.TryGetValue("source_hash", out var sh)
                ? sh.StringValue : "",
            SourceText: r.Payload.TryGetValue("source_text", out var st)
                ? st.StringValue : ""
        )).ToList();
    }

    /// <summary>全量遍历 PointId（对账用）。</summary>
    public async Task<IReadOnlyList<string>> ScrollAllIdsAsync(
        CancellationToken ct = default)
    {
        var ids = new List<string>();
        PointId? offset = null;

        while (true)
        {
            var batch = await _client.ScrollAsync(
                _options.CollectionName,
                limit: 256,
                offset: offset,
                payloadSelector: false,
                vectorsSelector: false,
                cancellationToken: ct);

            ids.AddRange(batch.Result.Select(p => p.Id.Uuid));
            offset = batch.NextPageOffset;
            if (offset is null || batch.Result.Count < 256) break;
        }

        return ids;
    }
}

public sealed record SemanticHit(
    string InodeId, float Score,
    string HostType, string SpaceId,
    string SourceHash, string SourceText);

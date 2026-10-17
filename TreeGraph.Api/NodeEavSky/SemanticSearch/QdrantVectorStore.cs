using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>
/// Qdrant 向量存储薄封装（双向量：节点向量 + 实体向量）。
///
/// PointId 规则（Qdrant 仅支持 UUID / uint）：
///   - 节点点：UUID = inodeId 本身
///   - 实体点：UUID = MD5("entity:" + entityId) 派生的确定性 UUID
///
/// payload 规则：
///   - point_kind：node / entity
///   - inode_id：路径关系（实体点通过它挂回节点）
///   - entity_id / entity_type：仅实体点
///
/// 节点与实体向量独立存在，互不影响权重与相似度。
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

    /// <summary>实体点的确定性 UUID（同 entityId 恒同 UUID，保证 upsert 幂等）。</summary>
    public static Guid EntityPointUuid(string entityId)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes("entity:" + entityId));
        return new Guid(bytes);
    }

    /// <summary>写入节点向量（纯节点文本）。</summary>
    public async Task UpsertNodeAsync(
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
                ["point_kind"] = "node",
                ["inode_id"] = inodeId,
                ["host_type"] = hostType,
                ["space_id"] = spaceId,
                ["source_hash"] = sourceHash,
                ["source_text"] = Truncate(sourceText)
            }
        };

        await _client.UpsertAsync(_options.CollectionName, [point],
            cancellationToken: ct);

        _logger.LogDebug("Qdrant upsert 节点：inode={InodeId} hash={Hash}",
            inodeId, sourceHash[..8]);
    }

    /// <summary>写入实体向量（实体独立文本，通过 inode_id 建立路径关系）。</summary>
    public async Task UpsertEntityAsync(
        string inodeId, string entityId, string entityType, float[] vector,
        string spaceId, string sourceHash, string sourceText,
        CancellationToken ct = default)
    {
        var point = new PointStruct
        {
            Id = new PointId { Uuid = EntityPointUuid(entityId).ToString() },
            Vectors = vector,
            Payload =
            {
                ["point_kind"] = "entity",
                ["inode_id"] = inodeId,
                ["entity_id"] = entityId,
                ["entity_type"] = entityType,
                ["host_type"] = "entity",
                ["space_id"] = spaceId,
                ["source_hash"] = sourceHash,
                ["source_text"] = Truncate(sourceText)
            }
        };

        await _client.UpsertAsync(_options.CollectionName, [point],
            cancellationToken: ct);

        _logger.LogDebug("Qdrant upsert 实体：inode={InodeId} entity={EntityId} type={EntityType}",
            inodeId, entityId, entityType);
    }

    /// <summary>删除某 inode 的全部向量（节点点 + 其全部实体点）。</summary>
    public async Task DeleteByInodeAsync(string inodeId, CancellationToken ct = default)
    {
        var filter = new Filter();
        filter.Must.Add(new Condition
        {
            Field = new FieldCondition
            {
                Key = "inode_id",
                Match = new Match { Keyword = inodeId }
            }
        });

        await _client.DeleteAsync(_options.CollectionName, filter,
            cancellationToken: ct);
        _logger.LogDebug("Qdrant delete by inode：{InodeId}", inodeId);
    }

    /// <summary>按 PointId（UUID）删除单个向量点（对账清理孤儿用）。</summary>
    public async Task DeletePointAsync(string uuid, CancellationToken ct = default)
    {
        await _client.DeleteAsync(_options.CollectionName,
            Guid.Parse(uuid),
            cancellationToken: ct);
        _logger.LogDebug("Qdrant delete point：{Uuid}", uuid);
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
            PointKind: GetString(r, "point_kind"),
            InodeId: GetString(r, "inode_id"),
            EntityId: GetString(r, "entity_id"),
            EntityType: GetString(r, "entity_type"),
            Score: r.Score,
            HostType: GetString(r, "host_type"),
            SpaceId: GetString(r, "space_id"),
            SourceHash: GetString(r, "source_hash"),
            SourceText: GetString(r, "source_text")
        )).ToList();
    }

    /// <summary>全量遍历向量点（对账用，含 point_kind / inode_id / entity_id）。</summary>
    public async Task<IReadOnlyList<StoredVectorPoint>> ScrollAllPointsAsync(
        CancellationToken ct = default)
    {
        var points = new List<StoredVectorPoint>();
        PointId? offset = null;

        while (true)
        {
            var batch = await _client.ScrollAsync(
                _options.CollectionName,
                limit: 256,
                offset: offset,
                payloadSelector: true,
                vectorsSelector: false,
                cancellationToken: ct);

            points.AddRange(batch.Result.Select(p => new StoredVectorPoint(
                Uuid: p.Id.Uuid,
                PointKind: PayloadString(p.Payload, "point_kind"),
                InodeId: PayloadString(p.Payload, "inode_id"),
                EntityId: PayloadString(p.Payload, "entity_id"))));

            offset = batch.NextPageOffset;
            if (offset is null || batch.Result.Count < 256) break;
        }

        return points;
    }

    private static string Truncate(string text) =>
        text.Length > 4000 ? text[..4000] : text;

    private static string GetString(ScoredPoint r, string key) =>
        r.Payload.TryGetValue(key, out var v) ? v.StringValue : "";

    private static string PayloadString(
        Google.Protobuf.Collections.MapField<string, Value> payload, string key) =>
        payload.TryGetValue(key, out var v) ? v.StringValue : "";
}

public sealed record SemanticHit(
    string PointKind, string InodeId,
    string? EntityId, string? EntityType,
    float Score,
    string HostType, string SpaceId,
    string SourceHash, string SourceText);

/// <summary>对账视角的已存向量点。</summary>
public sealed record StoredVectorPoint(
    string Uuid, string PointKind, string InodeId, string? EntityId);

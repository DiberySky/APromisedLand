using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>
/// Qdrant 向量存储薄封装（双向量：节点向量 + 实体向量）。
///
/// PointId 规则（Qdrant 仅支持 UUID / uint）：
///   - 节点点：UUID = MD5("node:{inodeId}:{viewType}") 派生的确定性 UUID
///     （按视图类型派生，模板升版即换 UUID，旧点由对账清理）
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

    /// <summary>节点点的确定性 UUID（同 inode+viewType 恒同 UUID，模板升版即换 UUID）。</summary>
    public static Guid NodePointUuid(string inodeId, string viewType)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"node:{inodeId}:{viewType}"));
        return new Guid(bytes);
    }

    /// <summary>行点的确定性 UUID（同 rowId 恒同 UUID，保证 upsert 幂等）。</summary>
    public static Guid RowPointUuid(string rowId)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes("row:" + rowId));
        return new Guid(bytes);
    }

    /// <summary>写入节点向量（path_view 两段式文本 + 路径快照 payload）。</summary>
    public async Task UpsertNodeAsync(
        string inodeId, float[] vector,
        string hostType, string spaceId, string sourceHash, string sourceText,
        string viewType, string templateVersion,
        string path, IReadOnlyList<string> pathIds, int depth,
        CancellationToken ct = default)
    {
        var pathIdsValue = new Value { ListValue = new ListValue() };
        pathIdsValue.ListValue.Values.AddRange(pathIds.Select(id => (Value)id));

        var point = new PointStruct
        {
            Id = new PointId { Uuid = NodePointUuid(inodeId, viewType).ToString() },
            Vectors = vector,
            Payload =
            {
                ["point_kind"] = "node",
                ["inode_id"] = inodeId,
                ["host_type"] = hostType,
                ["space_id"] = spaceId,
                ["source_hash"] = sourceHash,
                ["source_text"] = Truncate(sourceText),
                ["view_type"] = viewType,
                ["template_version"] = templateVersion,
                ["path"] = Truncate(path),
                ["path_ids"] = pathIdsValue,
                ["depth"] = (long)depth
            }
        };

        await _client.UpsertAsync(_options.CollectionName, [point],
            cancellationToken: ct);

        _logger.LogDebug("Qdrant upsert 节点：inode={InodeId} view={ViewType} hash={Hash}",
            inodeId, viewType, sourceHash[..8]);
    }

    /// <summary>
    /// 写入实体向量（实体独立文本，通过 inode_id 建立路径关系）。
    /// attrs 非空时把每个属性写入 payload 键 attr_{attributeId}（显示值），
    /// 供 hybrid 检索做属性精确过滤（Qdrant keyword match）。
    /// attrNums 非空时写入数值槽 attrn_{attributeId}（double），供 Range 范围过滤。
    /// </summary>
    public async Task UpsertEntityAsync(
        string inodeId, string entityId, string entityType, float[] vector,
        string spaceId, string sourceHash, string sourceText,
        IReadOnlyDictionary<string, string>? attrs = null,
        IReadOnlyDictionary<string, double>? attrNums = null,
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

        if (attrs is not null)
            foreach (var (attrId, display) in attrs)
                point.Payload[$"attr_{attrId}"] = display;

        if (attrNums is not null)
            foreach (var (attrId, num) in attrNums)
                point.Payload[$"attrn_{attrId}"] = num;

        await _client.UpsertAsync(_options.CollectionName, [point],
            cancellationToken: ct);

        _logger.LogDebug("Qdrant upsert 实体：inode={InodeId} entity={EntityId} type={EntityType}",
            inodeId, entityId, entityType);
    }

    /// <summary>写入行向量（自定义表行独立文本，entity_id/inode_id 建立归属链）。</summary>
    public async Task UpsertRowAsync(
        string rowId, string entityId, string entityType, string attributeId,
        float[] vector, string inodeId, string spaceId,
        string sourceHash, string sourceText,
        CancellationToken ct = default)
    {
        var point = new PointStruct
        {
            Id = new PointId { Uuid = RowPointUuid(rowId).ToString() },
            Vectors = vector,
            Payload =
            {
                ["point_kind"] = "row",
                ["row_id"] = rowId,
                ["entity_id"] = entityId,
                ["entity_type"] = entityType,
                ["attribute_id"] = attributeId,
                ["inode_id"] = inodeId,
                ["host_type"] = "row",
                ["space_id"] = spaceId,
                ["source_hash"] = sourceHash,
                ["source_text"] = Truncate(sourceText)
            }
        };

        await _client.UpsertAsync(_options.CollectionName, [point],
            cancellationToken: ct);

        _logger.LogDebug("Qdrant upsert 行：row={RowId} entity={EntityId} type={EntityType}",
            rowId, entityId, entityType);
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

    /// <summary>
    /// 删除某实体的全部行点（point_kind=row 且 entity_id 匹配；实体点不受影响）。
    /// 行同步先删后写用，清理已删行的残留点。
    /// </summary>
    public async Task DeleteRowsByEntityAsync(string entityId, CancellationToken ct = default)
    {
        var filter = new Filter();
        filter.Must.Add(new Condition
        {
            Field = new FieldCondition
            {
                Key = "point_kind",
                Match = new Match { Keyword = "row" }
            }
        });
        filter.Must.Add(new Condition
        {
            Field = new FieldCondition
            {
                Key = "entity_id",
                Match = new Match { Keyword = entityId }
            }
        });

        await _client.DeleteAsync(_options.CollectionName, filter,
            cancellationToken: ct);
        _logger.LogDebug("Qdrant delete rows by entity：{EntityId}", entityId);
    }

    /// <summary>按 PointId（UUID）删除单个向量点（对账清理孤儿用）。</summary>
    public async Task DeletePointAsync(string uuid, CancellationToken ct = default)
    {
        await _client.DeleteAsync(_options.CollectionName,
            Guid.Parse(uuid),
            cancellationToken: ct);
        _logger.LogDebug("Qdrant delete point：{Uuid}", uuid);
    }

    /// <summary>
    /// 向量检索。<paramref name="scoreThreshold"/> 非空时由 Qdrant 原生过滤低分点
    /// （低于该相似度的点不返回），调用方可据此少传候选、由上层再做相对过滤。
    /// attributeFilters：hybrid 属性等值过滤（AttributeId → 候选显示值数组，
    /// 单值=精确匹配，多值=MatchAny 即同属性内 OR），命中 attr_{id} 键；
    /// attributeRanges：数值范围过滤（attrn_{id} 键上的 Range）。
    /// 两类条件与空间过滤在 Filter.Must 顶层 AND。
    /// </summary>
    public async Task<IReadOnlyList<SemanticHit>> SearchAsync(
        float[] queryVector, int topK, string? spaceId,
        float? scoreThreshold = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? attributeFilters = null,
        IReadOnlyDictionary<string, AttrRangeFilter>? attributeRanges = null,
        CancellationToken ct = default)
    {
        Filter? filter = null;
        if (!string.IsNullOrWhiteSpace(spaceId))
        {
            filter ??= new Filter();
            filter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "space_id",
                    Match = new Match { Keyword = spaceId }
                }
            });
        }

        if (attributeFilters is not null)
        {
            filter ??= new Filter();
            foreach (var (attrId, expected) in attributeFilters)
            {
                if (expected.Count == 0) continue;
                var match = expected.Count == 1
                    ? new Match { Keyword = expected[0] }
                    : new Match { Keywords = new RepeatedStrings { Strings = { expected } } };
                filter.Must.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = $"attr_{attrId}",
                        Match = match
                    }
                });
            }
        }

        if (attributeRanges is not null)
        {
            filter ??= new Filter();
            foreach (var (attrId, range) in attributeRanges)
            {
                var r = new Qdrant.Client.Grpc.Range();
                if (range.Gte.HasValue) r.Gte = range.Gte.Value;
                if (range.Lte.HasValue) r.Lte = range.Lte.Value;
                if (range.Gt.HasValue) r.Gt = range.Gt.Value;
                if (range.Lt.HasValue) r.Lt = range.Lt.Value;
                filter.Must.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = $"attrn_{attrId}",
                        Range = r
                    }
                });
            }
        }

        var results = await _client.SearchAsync(
            _options.CollectionName,
            queryVector,
            filter: filter,
            limit: (ulong)topK,
            payloadSelector: true,
            scoreThreshold: scoreThreshold,
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
            SourceText: GetString(r, "source_text"),
            Path: GetString(r, "path"),
            PathIds: PayloadStrings(r.Payload, "path_ids")
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
                EntityId: PayloadString(p.Payload, "entity_id"),
                RowId: PayloadString(p.Payload, "row_id"),
                ViewType: PayloadString(p.Payload, "view_type"),
                TemplateVersion: PayloadString(p.Payload, "template_version"),
                Path: PayloadString(p.Payload, "path"),
                PathIds: PayloadStrings(p.Payload, "path_ids"),
                Depth: PayloadLong(p.Payload, "depth"))));

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

    private static IReadOnlyList<string>? PayloadStrings(
        Google.Protobuf.Collections.MapField<string, Value> payload, string key) =>
        payload.TryGetValue(key, out var v) && v.ListValue is not null
            ? v.ListValue.Values
                .Where(x => x.KindCase == Value.KindOneofCase.StringValue)
                .Select(x => x.StringValue)
                .ToList()
            : null;

    private static long? PayloadLong(
        Google.Protobuf.Collections.MapField<string, Value> payload, string key) =>
        payload.TryGetValue(key, out var v) &&
        v.KindCase == Value.KindOneofCase.IntegerValue
            ? v.IntegerValue
            : null;
}

public sealed record SemanticHit(
    string PointKind, string InodeId,
    string? EntityId, string? EntityType,
    float Score,
    string HostType, string SpaceId,
    string SourceHash, string SourceText,
    string Path = "",
    IReadOnlyList<string>? PathIds = null);

/// <summary>对账视角的已存向量点（节点点含视图/模板版本/路径快照，行点含 row_id）。</summary>
public sealed record StoredVectorPoint(
    string Uuid, string PointKind, string InodeId, string? EntityId,
    string? RowId = null,
    string? ViewType = null, string? TemplateVersion = null, string? Path = null,
    IReadOnlyList<string>? PathIds = null, long? Depth = null);

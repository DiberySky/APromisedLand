using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.GraphSyncSky;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.Messaging;
using TreeGraph.Shared.NodeSky.Entities;

namespace TreeGraph.Api.EdgeSky.Services;

/// <summary>
/// 边实例服务（镜像 NebulaGraph 语义）：
/// 唯一标识 = 五元组 &lt;SpaceId, EdgeType, SrcVid, Rank, DstVid&gt;（边从属于图空间）；
/// UPSERT 为覆盖语义（未列属性落 DEFAULT/NULL），PATCH 为部分 SET；
/// 属性值按所属 EdgeType 的 Schema 校验（未知键/类型不符 → 400）。
/// 写成功后发布 EdgeChangedEvent 触发图同步。
/// </summary>
public sealed class EdgeService
{
    private readonly TreeGraphDbContext _db;
    private readonly IEventPublisher _publisher;

    public EdgeService(TreeGraphDbContext db, IEventPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    /// <summary>
    /// 按 space_id / edge_type / src_vid / dst_vid 过滤查询；
    /// 另支持按节点双向过滤（nodeId + direction=out|in|either）与分页（limit/offset）。
    /// 返回体的 SrcName/DstName 为两端节点名（join nodes 补充，仅展示用）。
    /// </summary>
    public async Task<List<EdgeDto>> QueryAsync(
        string spaceId, string? edgeType, string? srcVid, string? dstVid,
        string? nodeId, string? direction, int? limit, int? offset, CancellationToken ct)
    {
        var q = _db.Edges.AsNoTracking().Where(e => e.SpaceId == spaceId);
        if (!string.IsNullOrEmpty(edgeType)) q = q.Where(e => e.EdgeType == edgeType);
        if (!string.IsNullOrEmpty(srcVid)) q = q.Where(e => e.SrcVid == srcVid);
        if (!string.IsNullOrEmpty(dstVid)) q = q.Where(e => e.DstVid == dstVid);

        if (!string.IsNullOrEmpty(nodeId))
        {
            q = direction switch
            {
                "out" => q.Where(e => e.SrcVid == nodeId),
                "in" => q.Where(e => e.DstVid == nodeId),
                _ => q.Where(e => e.SrcVid == nodeId || e.DstVid == nodeId)
            };
        }

        IQueryable<Edge> query = q.OrderBy(e => e.EdgeType).ThenBy(e => e.SrcVid)
            .ThenBy(e => e.Rank).ThenBy(e => e.DstVid);
        if (offset is > 0) query = query.Skip(offset.Value);
        if (limit is > 0) query = query.Take(limit.Value);

        var rows = await query.ToListAsync(ct);
        if (rows.Count == 0) return [];

        var nodeIds = rows.SelectMany(e => new[] { e.SrcVid, e.DstVid }).Distinct().ToList();
        var names = await _db.Nodes.AsNoTracking()
            .Where(n => nodeIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Name })
            .ToDictionaryAsync(n => n.Id, n => n.Name, StringComparer.Ordinal, ct);

        return rows.Select(e => ToDto(e, names)).ToList();
    }

    public async Task<EdgeDto> GetAsync(
        string spaceId, string edgeType, string srcVid, long rank, string dstVid, CancellationToken ct)
    {
        var edge = await _db.Edges.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SpaceId == spaceId && e.EdgeType == edgeType
                                      && e.SrcVid == srcVid && e.Rank == rank && e.DstVid == dstVid, ct);
        return edge is null
            ? throw new KeyNotFoundException(
                $"边不存在：{edgeType} {srcVid} -> {dstVid} @{rank}")
            : ToDto(edge);
    }

    /// <summary>
    /// UPSERT（覆盖语义，对齐 INSERT EDGE）：Props 为最终全集；
    /// 未列出的 Schema 属性落 DEFAULT，无 DEFAULT 则 NULL。
    /// </summary>
    public async Task<EdgeDto> UpsertAsync(
        string spaceId, string edgeType, string srcVid, long rank, string dstVid,
        UpsertEdgeRequest req, CancellationToken ct)
    {
        var schema = await LoadSchemaAsync(spaceId, edgeType, ct);
        await EnsureNodesExistAsync(spaceId, srcVid, dstVid, ct);

        var finalProps = ApplyUpsert(schema, req.Props);

        var edge = await _db.Edges.FindAsync([spaceId, edgeType, srcVid, rank, dstVid], ct);
        if (edge is null)
        {
            edge = new Edge { SpaceId = spaceId, EdgeType = edgeType, SrcVid = srcVid, Rank = rank, DstVid = dstVid };
            _db.Edges.Add(edge);
        }

        edge.Props = finalProps.Count > 0 ? JsonSerializer.SerializeToElement(finalProps) : null;
        edge.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await PublishAsync(edge, EdgeChangeKind.Upsert, ct);
        return ToDto(edge);
    }

    /// <summary>
    /// PATCH（部分更新，对齐 UPDATE EDGE SET）：仅更新列出的键；
    /// 值 null = 清除该属性（落 NULL；NOT NULL 属性不可清除）。
    /// </summary>
    public async Task<EdgeDto> PatchAsync(
        string spaceId, string edgeType, string srcVid, long rank, string dstVid,
        PatchEdgeRequest req, CancellationToken ct)
    {
        var schema = await LoadSchemaAsync(spaceId, edgeType, ct);

        var edge = await _db.Edges.FindAsync([spaceId, edgeType, srcVid, rank, dstVid], ct)
            ?? throw new KeyNotFoundException($"边不存在：{edgeType} {srcVid} -> {dstVid} @{rank}");

        var props = edge.Props is { } existing
            ? existing.Deserialize<Dictionary<string, JsonElement>>() ?? new()
            : new Dictionary<string, JsonElement>();

        foreach (var (key, value) in req.Props)
        {
            var prop = schema.FirstOrDefault(p => p.PropName == key)
                ?? throw new ArgumentException($"属性 '{key}' 未在边类型 '{edgeType}' 的 Schema 中定义");

            if (value is null or { ValueKind: JsonValueKind.Null })
            {
                if (!prop.Nullable)
                    throw new ArgumentException($"属性 '{key}' 为 NOT NULL，不可清除");
                props.Remove(key);
                continue;
            }

            var err = EdgePropValueValidator.Validate(prop.DataType, value.Value);
            if (err is not null)
                throw new ArgumentException($"属性 '{key}' 值无效：{err}");
            props[key] = value.Value;
        }

        EnsureNotNullSatisfied(schema, props);

        edge.Props = props.Count > 0 ? JsonSerializer.SerializeToElement(props) : null;
        edge.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await PublishAsync(edge, EdgeChangeKind.Upsert, ct);
        return ToDto(edge);
    }

    /// <summary>按五元组删除边（对齐 DELETE EDGE）。</summary>
    public async Task DeleteAsync(
        string spaceId, string edgeType, string srcVid, long rank, string dstVid, CancellationToken ct)
    {
        var edge = await _db.Edges.FindAsync([spaceId, edgeType, srcVid, rank, dstVid], ct)
            ?? throw new KeyNotFoundException($"边不存在：{edgeType} {srcVid} -> {dstVid} @{rank}");

        _db.Edges.Remove(edge);
        await _db.SaveChangesAsync(ct);

        await PublishAsync(edge, EdgeChangeKind.Delete, ct);
    }

    // ------------------------------------------------------------
    // 内部
    // ------------------------------------------------------------

    /// <summary>装载边类型属性 Schema（空间内）；类型不存在 → 404。</summary>
    private async Task<List<EdgeTypeProp>> LoadSchemaAsync(string spaceId, string edgeType, CancellationToken ct)
    {
        if (!await _db.EdgeTypes.AnyAsync(t => t.SpaceId == spaceId && t.Name == edgeType, ct))
            throw new KeyNotFoundException($"边类型 '{edgeType}' 不存在");
        return await _db.EdgeTypeProps.AsNoTracking()
            .Where(p => p.SpaceId == spaceId && p.EdgeTypeName == edgeType)
            .ToListAsync(ct);
    }

    /// <summary>两端必须都是 nodes 注册表中的真实节点，且归属同一图空间。</summary>
    private async Task EnsureNodesExistAsync(string spaceId, string srcVid, string dstVid, CancellationToken ct)
    {
        var srcSpace = await ResolveNodeSpaceIdAsync(srcVid, ct);
        if (srcSpace is null)
            throw new ArgumentException($"起点节点 '{srcVid}' 不存在于 nodes 注册表");
        if (srcSpace != spaceId)
            throw new ArgumentException($"起点节点 '{srcVid}' 所属空间 '{srcSpace}' 与边空间 '{spaceId}' 不一致");

        var dstSpace = await ResolveNodeSpaceIdAsync(dstVid, ct);
        if (dstSpace is null)
            throw new ArgumentException($"终点节点 '{dstVid}' 不存在于 nodes 注册表");
        if (dstSpace != spaceId)
            throw new ArgumentException($"终点节点 '{dstVid}' 所属空间 '{dstSpace}' 与边空间 '{spaceId}' 不一致");
    }

    /// <summary>覆盖语义组装最终属性包：请求值 + Schema 默认值补齐；未知键/类型不符 → 400。</summary>
    private static Dictionary<string, JsonElement> ApplyUpsert(
        List<EdgeTypeProp> schema, Dictionary<string, JsonElement>? requestProps)
    {
        var finalProps = new Dictionary<string, JsonElement>();

        foreach (var (key, value) in requestProps ?? [])
        {
            var prop = schema.FirstOrDefault(p => p.PropName == key)
                ?? throw new ArgumentException($"属性 '{key}' 未在边类型 Schema 中定义");

            if (value.ValueKind == JsonValueKind.Null)
            {
                if (!prop.Nullable)
                    throw new ArgumentException($"属性 '{key}' 为 NOT NULL，不可置 null");
                continue; // 显式 null = 落 NULL（不存储该键）
            }

            var err = EdgePropValueValidator.Validate(prop.DataType, value);
            if (err is not null)
                throw new ArgumentException($"属性 '{key}' 值无效：{err}");
            finalProps[key] = value;
        }

        // 未列属性落 DEFAULT（无 DEFAULT 则 NULL = 不存储该键）
        foreach (var p in schema)
            if (!finalProps.ContainsKey(p.PropName) && p.DefaultValue is { } dv)
                finalProps[p.PropName] = dv;

        EnsureNotNullSatisfied(schema, finalProps);
        return finalProps;
    }

    private static void EnsureNotNullSatisfied(
        List<EdgeTypeProp> schema, Dictionary<string, JsonElement> props)
    {
        foreach (var p in schema.Where(p => !p.Nullable))
            if (!props.TryGetValue(p.PropName, out var v) || v.ValueKind == JsonValueKind.Null)
                throw new ArgumentException($"属性 '{p.PropName}' 为 NOT NULL，须提供值或配置默认值");
    }

    private async Task PublishAsync(Edge edge, EdgeChangeKind kind, CancellationToken ct)
        => await _publisher.Publish(new EdgeChangedEvent
        {
            SpaceId = edge.SpaceId,
            EdgeType = edge.EdgeType,
            SrcVid = edge.SrcVid,
            Rank = edge.Rank,
            DstVid = edge.DstVid,
            Kind = kind
        }, ct);

    /// <summary>
    /// 解析节点所属图空间（节点关系维护入口）：按宿主类型路由 ——
    /// 单位树→「单位」空间、分类树→「类型」空间、知识树→「知识库」空间、
    /// 空间树→沿 ParentId 上溯的根（根 Id 即 Space.Id）、线路/线路节点→lines.space_id。
    /// 自由 Node 等无归属节点返回 null。
    /// </summary>
    public async Task<NodeSpaceDto?> GetNodeSpaceAsync(string nodeId, CancellationToken ct)
    {
        var spaceId = await ResolveNodeSpaceIdAsync(nodeId, ct);
        if (spaceId is null) return null;

        var space = await _db.Spaces.AsNoTracking()
            .Where(s => s.Id == spaceId)
            .Select(s => new { s.Id, s.Name, s.Type })
            .FirstOrDefaultAsync(ct);
        if (space is null) return null;

        return new NodeSpaceDto
        {
            SpaceId = space.Id,
            SpaceName = space.Name,
            SpaceType = space.Type.ToString()
        };
    }

    /// <summary>按名称关键字搜索节点（对端节点选择用）；空关键字返回前 200 条。仅投影 Id/Name/HostType。</summary>
    public async Task<List<NodeLookupDto>> SearchNodesAsync(string? search, CancellationToken ct)
    {
        var q = _db.Nodes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            q = q.Where(n => n.Name != null && n.Name.Contains(kw));
        }
        return await q.OrderBy(n => n.Name).Take(200)
            .Select(n => new NodeLookupDto { Id = n.Id, Name = n.Name, HostType = n.HostType })
            .ToListAsync(ct);
    }

    /// <summary>
    /// 解析节点所属图空间 Id（按宿主类型路由）；节点不存在或无归属返回 null。
    /// 供 GetNodeSpaceAsync 与 EnsureNodesExistAsync 共用，避免逻辑重复。
    /// </summary>
    private async Task<string?> ResolveNodeSpaceIdAsync(string nodeId, CancellationToken ct)
    {
        var row = await _db.Nodes.AsNoTracking()
            .Where(n => n.Id == nodeId)
            .Select(n => new { n.HostType, n.LineId })
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;

        return row.HostType switch
        {
            "UnitTree" => SeedSpaceIds.Unit,
            "CategoryTree" => SeedSpaceIds.Type,
            "KnowledgeTree" => SeedSpaceIds.Knowledge,
            "SpaceTree" => await ResolveSpaceTreeRootAsync(nodeId, ct),
            "Line" => await _db.Lines.AsNoTracking()
                .Where(l => l.Id == nodeId)
                .Select(l => l.SpaceId)
                .FirstOrDefaultAsync(ct),
            "LineNode" => row.LineId is null
                ? null
                : await _db.Lines.AsNoTracking()
                    .Where(l => l.Id == row.LineId)
                    .Select(l => l.SpaceId)
                    .FirstOrDefaultAsync(ct),
            _ => null
        };
    }

    /// <summary>空间树根祖先解析（根节点 Id 即 Space.Id）；递归 CTE 单次查询替代 N+1 上溯。</summary>
    private async Task<string> ResolveSpaceTreeRootAsync(string nodeId, CancellationToken ct)
    {
        var root = await _db.SpaceTrees
            .FromSqlRaw(@"
                WITH RECURSIVE tree AS (
                    SELECT id, parent_id FROM space_trees WHERE id = {0}
                    UNION ALL
                    SELECT st.id, st.parent_id FROM space_trees st
                    INNER JOIN tree t ON st.id = t.parent_id
                )
                SELECT id, parent_id FROM tree WHERE parent_id IS NULL
                LIMIT 1", nodeId)
            .Select(st => st.Id)
            .FirstOrDefaultAsync(ct);
        return root ?? nodeId;
    }

    private static EdgeDto ToDto(Edge e, IReadOnlyDictionary<string, string?>? names = null) => new()
    {
        SpaceId = e.SpaceId,
        EdgeType = e.EdgeType,
        SrcVid = e.SrcVid,
        Rank = e.Rank,
        DstVid = e.DstVid,
        Props = e.Props is { } p ? p.Deserialize<Dictionary<string, JsonElement>>() : null,
        SrcName = names?.GetValueOrDefault(e.SrcVid),
        DstName = names?.GetValueOrDefault(e.DstVid),
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };
}

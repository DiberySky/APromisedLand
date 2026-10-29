using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TreeGraph.Api.Data;
using TreeGraph.Shared.Messaging;

namespace TreeGraph.Api.GraphSyncSky;

/// <summary>
/// 图内节点快照（跨 4 棵宿主树 + LineNode/Line 解析结果）。
/// SpaceId = 归属图空间（PG Space.Id）；null = 全局自由 Node（同步到全部空间）。
/// </summary>
public sealed record GraphNodeInfo(
    string Id, string Name, string Tag, string NodeType, bool IsArchived, string? ParentId,
    IReadOnlyDictionary<string, object?>? ExtraProps = null,
    string? SpaceId = null);

/// <summary>全量重建结果统计。</summary>
public sealed record GraphRebuildResult(
    int Vertices, int ParentEdges, int LineEdges, int SemanticEdges,
    int VerticesWritten, int EdgesWritten, IReadOnlyList<string> Errors);

/// <summary>
/// 图同步器：统一 consumer / 全量重建端点的同步逻辑。
/// PG 空间 ↔ Nebula 空间 1:1（<see cref="GraphNaming.ToNebulaSpace"/> 映射）；
/// 顶点按宿主路由到归属空间（单位树→「单位」空间、分类树→「类型」、知识树→「知识库」、
/// 空间树→根节点对应动态空间、线路/线路节点→线路所属空间），自由 Node 同步到全部空间；
/// 边按 evt.SpaceId / edges.space_id 路由。消息只带 Id，状态从 PostgreSQL 现取，同步幂等。
/// </summary>
public sealed class GraphSynchronizer
{
    private readonly TreeGraphDbContext _db;
    private readonly NebulaGraphClient _client;
    private readonly GraphSchemaInitializer _schemaInitializer;
    private readonly GraphSyncOptions _options;
    private readonly ILogger<GraphSynchronizer> _logger;

    public GraphSynchronizer(
        TreeGraphDbContext db,
        NebulaGraphClient client,
        GraphSchemaInitializer schemaInitializer,
        IOptions<GraphSyncOptions> options,
        ILogger<GraphSynchronizer> logger)
    {
        _db = db;
        _client = client;
        _schemaInitializer = schemaInitializer;
        _options = options.Value;
        _logger = logger;
    }

    // ============================================================
    // 顶点（iNode）
    // ============================================================

    /// <summary>同步单个 inode 顶点 + 其 parent_of 边。返回是否写入（false = 已删除）。</summary>
    public async Task<bool> SyncInodeAsync(string inodeId, CancellationToken ct = default)
    {
        var node = await ResolveNodeAsync(inodeId, ct);
        if (node is null)
        {
            // 顶点已不存在：全部空间各删一遍（幂等）
            foreach (var space in await AllNebulaSpacesAsync(ct))
                await _client.DeleteVertexAsync(space, inodeId, ct);
            _logger.LogInformation("图顶点已删除：{InodeId}", inodeId);
            return false;
        }

        foreach (var space in await RouteSpacesAsync(node, ct))
        {
            await _client.UpsertVertexAsync(space, node.Id, node.Tag, VertexProps(node), ct);

            await SyncParentEdgeAsync(space, node, ct);

            // 线路节点关联：connects_to（node → 外部 inode，属性 role；无端子/无外部连接时清空）。
            // R7：Line→LineNode 归属走 parent_of（SyncParentEdgeAsync），原归属边类型已下线；
            //     line_segment 段边为 edges 表内置边类型，走 EdgeGraphSyncConsumer 管道。
            if (node.Tag == GraphNaming.LineNodeTag)
                await SyncLineNodeLinksAsync(space, node, ct);
        }

        _logger.LogDebug("图顶点已同步：{InodeId} tag={Tag}", node.Id, node.Tag);
        return true;
    }

    /// <summary>节点归属的 Nebula 空间集：SpaceId 非空 = 单空间；null（自由 Node）= 全部空间。</summary>
    private async Task<IReadOnlyList<string>> RouteSpacesAsync(GraphNodeInfo node, CancellationToken ct)
        => node.SpaceId is not null
            ? [GraphNaming.ToNebulaSpace(node.SpaceId)]
            : await AllNebulaSpacesAsync(ct);

    private async Task<List<string>> AllNebulaSpacesAsync(CancellationToken ct)
        => (await _db.Spaces.AsNoTracking().Select(s => s.Id).ToListAsync(ct))
            .Select(GraphNaming.ToNebulaSpace)
            .ToList();

    private static Dictionary<string, object?> VertexProps(GraphNodeInfo node)
    {
        var props = new Dictionary<string, object?>
        {
            ["name"] = node.Name,
            ["node_type"] = node.NodeType,
            ["is_archived"] = node.IsArchived
        };
        if (node.ExtraProps is not null)
            foreach (var (k, v) in node.ExtraProps)
                props[k] = v;
        return props;
    }

    /// <summary>对齐 Nebula parent_of 边：删除不再是当前父的残留边，补上缺失的当前父边。</summary>
    private async Task SyncParentEdgeAsync(string space, GraphNodeInfo node, CancellationToken ct)
    {
        var result = await _client.QueryAsync(space,
            $"GO FROM \"{node.Id}\" OVER `{GraphNaming.ParentOfEdge}` REVERSELY " +
            "YIELD src(edge) AS parent", ct);

        var existing = result.Data?
            .Select(r => r.TryGetValue("parent", out var v) ? v.GetString() : null)
            .Where(s => !string.IsNullOrEmpty(s))
            .Cast<string>()
            .ToList() ?? [];

        foreach (var parent in existing.Where(p => p != node.ParentId))
            await _client.DeleteEdgeAsync(space, GraphNaming.ParentOfEdge, parent, node.Id, 0, ct);

        if (node.ParentId is not null && !existing.Contains(node.ParentId))
            await _client.UpsertEdgeAsync(space, GraphNaming.ParentOfEdge, node.ParentId, node.Id, 0, null, ct);
    }

    /// <summary>
    /// 线路节点关联边：connects_to（node → 外部 inode，属性 role；无端子/无外部连接时清空）。
    /// R7：原归属边类型的对齐段已删除（归属走 parent_of）。
    /// </summary>
    private async Task SyncLineNodeLinksAsync(string space, GraphNodeInfo node, CancellationToken ct)
    {
        // 外部连接：端子表为准（一个节点同属一条线路，故至多一条端子）
        var terminal = await _db.LineTerminals.AsNoTracking()
            .Where(t => t.NodeId == node.Id)
            .Select(t => new { t.Role, t.OperatingState, t.ExternalNodeId })
            .FirstOrDefaultAsync(ct);

        var connects = await _client.QueryAsync(space,
            $"GO FROM \"{node.Id}\" OVER `{GraphNaming.ConnectsToEdge}` " +
            "YIELD dst(edge) AS target", ct);
        var existingTargets = QueryColumn(connects, "target");

        foreach (var old in existingTargets.Where(x => x != terminal?.ExternalNodeId))
            await _client.DeleteEdgeAsync(space, GraphNaming.ConnectsToEdge, node.Id, old, 0, ct);

        if (terminal?.ExternalNodeId is not null
            && !existingTargets.Contains(terminal.ExternalNodeId))
        {
            await _client.UpsertEdgeAsync(space,
                GraphNaming.ConnectsToEdge, node.Id, terminal.ExternalNodeId, 0,
                new Dictionary<string, object?>
                {
                    ["role"] = terminal.Role.ToString(),
                    ["operating_state"] = terminal.OperatingState.ToString()
                }, ct);
        }
    }

    private static List<string> QueryColumn(NebulaQueryResult result, string column)
        => result.Data?
            .Select(r => r.TryGetValue(column, out var v) ? v.GetString() : null)
            .Where(s => !string.IsNullOrEmpty(s))
            .Cast<string>()
            .ToList() ?? [];

    /// <summary>
    /// 解析节点：先单查 nodes 身份行（name/host_type），再按 host_type 只查对应扩展表的附加属性。
    /// 不再逐一试各个宿主守卫。SpaceId 按宿主路由规则解析（自由 Node = null）。
    /// </summary>
    private async Task<GraphNodeInfo?> ResolveNodeAsync(string inodeId, CancellationToken ct)
    {
        var row = await _db.Nodes.AsNoTracking()
            .Where(n => n.Id == inodeId)
            .Select(n => new { n.Id, n.Name, n.HostType, n.LineId })
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;

        switch (row.HostType)
        {
            case "SpaceTree":
            {
                var parentId = await _db.SpaceTrees.AsNoTracking()
                    .Where(n => n.Id == inodeId)
                    .Select(n => n.ParentId)
                    .FirstOrDefaultAsync(ct);
                // 空间树节点归属空间 = 根祖先（根节点 Id 即 Space.Id）
                var rootSpaceId = await ResolveSpaceTreeRootAsync(inodeId, ct);
                return new GraphNodeInfo(inodeId, row.Name, GraphNaming.SpaceTreeTag,
                    "SpaceTree", false, parentId, null, rootSpaceId);
            }
            case "UnitTree":
            {
                var parentId = await _db.UnitTrees.AsNoTracking()
                    .Where(n => n.Id == inodeId)
                    .Select(n => n.ParentId)
                    .FirstOrDefaultAsync(ct);
                return new GraphNodeInfo(inodeId, row.Name, GraphNaming.UnitTreeTag,
                    "UnitTree", false, parentId, null, SeedSpaceIds.Unit);
            }
            case "CategoryTree":
            {
                var h = await _db.CategoryTrees.AsNoTracking()
                    .Where(n => n.Id == inodeId)
                    .Select(n => new { n.ParentId, n.IsArchived })
                    .FirstOrDefaultAsync(ct);
                return new GraphNodeInfo(inodeId, row.Name, GraphNaming.CategoryTreeTag,
                    "CategoryTree", h?.IsArchived ?? false, h?.ParentId, null, SeedSpaceIds.Type);
            }
            case "KnowledgeTree":
            {
                var h = await _db.KnowledgeTrees.AsNoTracking()
                    .Where(n => n.Id == inodeId)
                    .Select(n => new { n.ParentId, n.IsArchived })
                    .FirstOrDefaultAsync(ct);
                return new GraphNodeInfo(inodeId, row.Name, GraphNaming.KnowledgeTreeTag,
                    "KnowledgeTree", h?.IsArchived ?? false, h?.ParentId, null, SeedSpaceIds.Knowledge);
            }
            case "LineNode":
            {
                // R7：line_id 即 nodes.line_id（身份锚点）；parent_of 边指向所属 Line
                var lineId = row.LineId;
                if (lineId is null) return null;

                var line = await _db.Lines.AsNoTracking()
                    .Where(l => l.Id == lineId)
                    .Select(l => new { l.SpaceId })
                    .FirstOrDefaultAsync(ct);
                if (line is null) return null; // lines 行缺失 = 数据异常，不同步

                var lineName = await _db.Nodes.AsNoTracking()
                    .Where(l => l.Id == lineId)
                    .Select(l => l.Name)
                    .FirstOrDefaultAsync(ct) ?? string.Empty;
                var role = await _db.LineTerminals.AsNoTracking()
                    .Where(t => t.LineId == lineId && t.NodeId == inodeId)
                    .Select(t => new
                    {
                        Role = (TreeGraph.Shared.LineSky.Entities.TerminalRole?)t.Role,
                        OperatingState = (TreeGraph.Shared.LineSky.Entities.TerminalOperatingState?)t.OperatingState
                    })
                    .FirstOrDefaultAsync(ct);

                return new GraphNodeInfo(inodeId, row.Name, GraphNaming.LineNodeTag,
                    "LineNode", false, lineId, new Dictionary<string, object?>
                    {
                        ["line_id"] = lineId,
                        ["line_name"] = lineName,
                        ["terminal_role"] = role?.Role?.ToString(),
                        ["terminal_operating_state"] = role?.OperatingState?.ToString()
                    }, line.SpaceId);
            }
            case "Line":
            {
                // R7：Line 注册在 nodes，顶点链路直连；拓扑模式已下线（结构由边连接定义）
                var lineSpaceId = await _db.Lines.AsNoTracking()
                    .Where(l => l.Id == inodeId)
                    .Select(l => l.SpaceId)
                    .FirstOrDefaultAsync(ct);
                if (lineSpaceId is null) return null; // lines 行缺失 = 数据异常，不同步
                return new GraphNodeInfo(inodeId, row.Name, GraphNaming.LineTag,
                    "Line", false, null, null, lineSpaceId);
            }
            case "Node":
                // 自由节点：无宿主扩展表，只入身份与名称；同步到全部空间
                return new GraphNodeInfo(inodeId, row.Name, GraphNaming.NodeTag,
                    "Node", false, null);

            default:
                // 其它宿主身份暂不入图（R4 不做超边/边顶点）
                return null;
        }
    }

    /// <summary>空间树根祖先解析（根节点 Id 即 Space.Id）；带深度上限防环。</summary>
    private async Task<string> ResolveSpaceTreeRootAsync(string nodeId, CancellationToken ct)
    {
        var current = nodeId;
        for (var depth = 0; depth < 64; depth++)
        {
            var parent = await _db.SpaceTrees.AsNoTracking()
                .Where(n => n.Id == current)
                .Select(n => n.ParentId)
                .FirstOrDefaultAsync(ct);
            if (parent is null) return current;
            current = parent;
        }
        return current;
    }

    // ============================================================
    // 语义边（edges 表，EdgeSky 顶级实体）
    // ============================================================

    /// <summary>
    /// 同步单条边（五元组定位，按 evt.SpaceId 路由到目标图空间）。
    /// Delete 时 PG 行已不存在，依赖事件载荷定位图边；
    /// Upsert 回查 PG edges 读 props 整包投图（事件在 SaveChanges 成功后发布，PG 即最新状态；
    /// 行已不存在 = 事件乱序/已删，按删除兜底，幂等）。写边前确保两端顶点在该空间存在。
    /// </summary>
    public async Task SyncEdgeAsync(EdgeChangedEvent evt, CancellationToken ct = default)
    {
        var space = GraphNaming.ToNebulaSpace(evt.SpaceId);

        if (evt.Kind == EdgeChangeKind.Delete)
        {
            await _client.DeleteEdgeAsync(space, evt.EdgeType, evt.SrcVid, evt.DstVid, evt.Rank, ct);
            _logger.LogInformation("图边已删除：{Space} {Type} {Src}->{Dst}@{Rank}",
                space, evt.EdgeType, evt.SrcVid, evt.DstVid, evt.Rank);
            return;
        }

        var row = await _db.Edges.AsNoTracking()
            .Where(e => e.SpaceId == evt.SpaceId && e.EdgeType == evt.EdgeType
                     && e.SrcVid == evt.SrcVid && e.Rank == evt.Rank && e.DstVid == evt.DstVid)
            .Select(e => new { e.Props })
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            await _client.DeleteEdgeAsync(space, evt.EdgeType, evt.SrcVid, evt.DstVid, evt.Rank, ct);
            _logger.LogInformation("图边按删除兜底（PG 行不存在）：{Space} {Type} {Src}->{Dst}@{Rank}",
                space, evt.EdgeType, evt.SrcVid, evt.DstVid, evt.Rank);
            return;
        }

        // 写边前确保两端顶点在该空间存在（跨空间引用时顶点可能尚未同步到本空间）
        await EnsureVertexInSpaceAsync(space, evt.SrcVid, ct);
        await EnsureVertexInSpaceAsync(space, evt.DstVid, ct);

        await _client.UpsertEdgeAsync(space, evt.EdgeType, evt.SrcVid, evt.DstVid, evt.Rank,
            PropsToDict(row.Props), ct);
        _logger.LogDebug("图边已同步：{Space} {Type} {Src}->{Dst}@{Rank}",
            space, evt.EdgeType, evt.SrcVid, evt.DstVid, evt.Rank);
    }

    /// <summary>确保顶点在指定空间存在（按 nodes 注册表现解析；已删除则跳过）。</summary>
    private async Task EnsureVertexInSpaceAsync(string space, string vid, CancellationToken ct)
    {
        var node = await ResolveNodeAsync(vid, ct);
        if (node is null) return;
        await _client.UpsertVertexAsync(space, node.Id, node.Tag, VertexProps(node), ct);
    }

    /// <summary>props jsonb → 属性字典（JsonElement 直传，PostAsJsonAsync 原生序列化；null 值跳过）。</summary>
    internal static Dictionary<string, object?>? PropsToDict(JsonElement? props)
    {
        if (props is not { } p || p.ValueKind != JsonValueKind.Object) return null;
        var dict = new Dictionary<string, object?>();
        foreach (var prop in p.EnumerateObject())
            if (prop.Value.ValueKind != JsonValueKind.Null)
                dict[prop.Name] = prop.Value;
        return dict.Count == 0 ? null : dict;
    }

    // ============================================================
    // 全量重建 / 按类别同步
    // ============================================================

    /// <summary>
    /// 全量重建：逐空间 DROP SPACE → 重建全部空间 schema → 从 PG 全量装载顶点/边按空间批量写入。
    /// 写失败批次计入 Errors，不中断整体流程。
    /// </summary>
    public async Task<GraphRebuildResult> RebuildAllAsync(CancellationToken ct)
    {
        var spaceIds = await _db.Spaces.AsNoTracking().Select(s => s.Id).ToListAsync(ct);

        foreach (var sid in spaceIds)
            await _client.QueryAsync($"DROP SPACE IF EXISTS `{GraphNaming.ToNebulaSpace(sid)}`", ct);
        await _schemaInitializer.RecreateSchemaAsync(ct);

        var ctx = await LoadGraphContextAsync(ct);

        return await SyncSpacesAsync(spaceIds, sid => BuildSpaceBatch(ctx, sid,
            includeSpaceTree: true, includeTrees: true, includeFreeNodes: true,
            includeLines: true, includeSemantic: true), "全量重建", ct);
    }

    /// <summary>
    /// 按类别同步（同步刷新对话框用）：把 PG 中该类别现状批量覆盖写入 NebulaGraph。
    /// 幂等覆盖写，不 DROP SPACE；图内残留清理仍靠事件同步 / 全量重建。
    /// </summary>
    public async Task<GraphRebuildResult> SyncCategoryAsync(SyncCategory category, CancellationToken ct)
    {
        var spaceIds = await _db.Spaces.AsNoTracking().Select(s => s.Id).ToListAsync(ct);
        var ctx = await LoadGraphContextAsync(ct);

        return await SyncSpacesAsync(spaceIds, category switch
        {
            SyncCategory.Space => sid => BuildSpaceBatch(ctx, sid, includeSpaceTree: true),
            SyncCategory.Tree => sid => BuildSpaceBatch(ctx, sid, includeTrees: true),
            SyncCategory.Line => sid => BuildSpaceBatch(ctx, sid, includeLines: true),
            SyncCategory.Edge => sid => BuildSpaceBatch(ctx, sid, includeSemantic: true),
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        }, category.ToString(), ct);
    }

    /// <summary>逐空间构建批次并 bulk 写入，聚合统计（不 DROP，schema 由启动对齐/重建保证）。</summary>
    private async Task<GraphRebuildResult> SyncSpacesAsync(
        IReadOnlyList<string> spaceIds,
        Func<string, SpaceBatch> buildBatch,
        string logLabel,
        CancellationToken ct)
    {
        var vertices = 0;
        var parentEdges = 0;
        var lineEdges = 0;
        var semanticEdges = 0;
        var verticesWritten = 0;
        var edgesWritten = 0;
        var errors = new List<string>();

        foreach (var sid in spaceIds)
        {
            var batch = buildBatch(sid);
            if (batch.Vertices.Count == 0 && batch.Edges.Count == 0)
                continue;

            var result = await BulkWithSchemaRetryAsync(
                GraphNaming.ToNebulaSpace(sid), batch.Vertices, batch.Edges, ct);

            vertices += batch.Vertices.Count;
            parentEdges += batch.ParentEdges;
            lineEdges += batch.LineEdges;
            semanticEdges += batch.SemanticEdges;
            verticesWritten += result.VerticesSuccess;
            edgesWritten += result.EdgesSuccess;
            CollectErrors(result, errors);
        }

        _logger.LogInformation(
            "图{Label}：顶点 {V}/{VT}，边 {E}/{ET}，错误批次 {Err}",
            logLabel, verticesWritten, vertices, edgesWritten,
            parentEdges + lineEdges + semanticEdges, errors.Count);

        return new GraphRebuildResult(
            vertices, parentEdges, lineEdges, semanticEdges,
            verticesWritten, edgesWritten, errors);
    }

    private static void CollectErrors(BulkResult result, List<string> errors)
    {
        foreach (var err in result.VerticesErrors ?? [])
            errors.Add($"vertices: {err.GetValueOrDefault("error")}");
        foreach (var err in result.EdgesErrors ?? [])
            errors.Add($"edges: {err.GetValueOrDefault("error")}");
    }

    private async Task<BulkResult> BulkWithSchemaRetryAsync(
        string space, List<VertexItem> vertices, List<EdgeItem> edges, CancellationToken ct)
    {
        const int maxAttempts = 12; // 首次 + 11 次 × 5s ≈ 覆盖 55s 传播窗口
        for (var attempt = 1; ; attempt++)
        {
            var result = await _client.BulkAsync(space, vertices, edges, _options.RebuildBatchSize, ct);
            var allErrors = (result.VerticesErrors ?? []).Concat(result.EdgesErrors ?? [])
                .Select(e => e.TryGetValue("error", out var v) ? v.GetString() ?? "" : "")
                .ToList();

            if (allErrors.Count == 0 || !allErrors.Any(IsRetryableBulkError) || attempt >= maxAttempts)
                return result;

            _logger.LogWarning(
                "图批量写入遇到瞬时错误（schema 传播延迟 / storage 重选主，第 {Attempt}/{Max} 次），5s 后整体重试",
                attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    /// <summary>
    /// 判断是否为可重试的瞬时错误。
    /// ① schema 传播延迟：DROP SPACE 重建后 storaged 侧 space 信息（含 vid 类型）刷新滞后，
    ///    写入会报 No schema found / EdgeNotFound / vid 长度不符。
    /// ② storage 选主未完成：DROP SPACE 触发分片 leader 重选，紧随其后的写入会报
    ///    "Not the leader of N. Please retry later."，重选完成后即可写入。
    /// 两类均为 bulk 幂等覆盖写，整体重试安全。
    /// </summary>
    private static bool IsRetryableBulkError(string msg) =>
        msg.Contains("No schema found") || msg.Contains("EdgeNotFound")
        || msg.Contains("TagNotFound") || msg.Contains("SpaceNotFound")
        || msg.Contains("Invalid space vid len")
        || msg.Contains("Not the leader") || msg.Contains("Storage Error")
        || msg.Contains("LeaderChanged") || msg.Contains("retry later");

    // ============================================================
    // 全量装载（PG → 内存快照 → 按空间分桶）
    // ============================================================

    private sealed record SpaceTreeRow(string Id, string? ParentId, string RootSpaceId);
    private sealed record TreeRow(string Id, string? ParentId, bool IsArchived);
    private sealed record LineRow(string Id, string Name, string SpaceId);
    private sealed record LineNodeRow(
        string Id, string Name, string LineId, string LineSpaceId,
        string? TerminalRole, string? TerminalState);
    private sealed record SemanticEdgeRow(
        string SpaceId, string SrcVid, string DstVid, string EdgeType, long Rank, JsonElement? Props);

    /// <summary>PG 全量快照（单趟装载，按空间分桶前的共享上下文）。</summary>
    private sealed record GraphContext(
        IReadOnlyDictionary<string, string?> NodeNames,
        List<SpaceTreeRow> SpaceTrees,
        List<TreeRow> UnitTrees,
        List<TreeRow> CategoryTrees,
        List<TreeRow> KnowledgeTrees,
        List<(string Id, string? Name)> FreeNodes,
        List<LineRow> Lines,
        List<LineNodeRow> LineNodes,
        List<(string NodeId, string ExternalNodeId, string Role, string OperatingState)> ConnectsTo,
        List<SemanticEdgeRow> SemanticEdges);

    private async Task<GraphContext> LoadGraphContextAsync(CancellationToken ct)
    {
        var nodeNames = await _db.Nodes.AsNoTracking()
            .ToDictionaryAsync(n => n.Id, n => n.Name, ct);

        // 空间树：内存解析根祖先（根 Id = Space.Id）
        var spaceTreeRaw = await _db.SpaceTrees.AsNoTracking()
            .Select(n => new { n.Id, n.ParentId })
            .ToListAsync(ct);
        var parentMap = spaceTreeRaw.ToDictionary(x => x.Id, x => x.ParentId);
        var spaceTrees = spaceTreeRaw
            .Select(x => new SpaceTreeRow(x.Id, x.ParentId, ResolveRoot(x.Id)))
            .ToList();

        string ResolveRoot(string id)
        {
            var cur = id;
            for (var depth = 0; depth < 64 && parentMap.TryGetValue(cur, out var p) && p is not null; depth++)
                cur = p;
            return cur;
        }

        var unitTrees = await _db.UnitTrees.AsNoTracking()
            .Select(n => new TreeRow(n.Id, n.ParentId, false))
            .ToListAsync(ct);
        var categoryTrees = await _db.CategoryTrees.AsNoTracking()
            .Select(n => new TreeRow(n.Id, n.ParentId, n.IsArchived))
            .ToListAsync(ct);
        var knowledgeTrees = await _db.KnowledgeTrees.AsNoTracking()
            .Select(n => new TreeRow(n.Id, n.ParentId, n.IsArchived))
            .ToListAsync(ct);

        var freeNodes = await _db.Nodes.AsNoTracking()
            .Where(x => x.HostType == "Node")
            .Select(x => new ValueTuple<string, string?>(x.Id, x.Name))
            .ToListAsync(ct);

        // 线路：名称唯一真源在 nodes；空间归属在 lines 表
        var lines = await (
            from n in _db.Nodes.AsNoTracking()
            where n.HostType == "Line"
            join l in _db.Lines.AsNoTracking() on n.Id equals l.Id
            select new LineRow(n.Id, n.Name ?? "", l.SpaceId)).ToListAsync(ct);
        var lineSpaceMap = lines.ToDictionary(l => l.Id, l => l.SpaceId);

        var terminalInfo = await _db.LineTerminals.AsNoTracking()
            .Select(t => new { t.LineId, t.NodeId, t.Role, t.OperatingState, t.ExternalNodeId })
            .ToListAsync(ct);
        var terminalRoleMap = terminalInfo.ToDictionary(
            x => (x.LineId, x.NodeId), x => x.Role.ToString());
        var terminalStateMap = terminalInfo.ToDictionary(
            x => (x.LineId, x.NodeId), x => x.OperatingState.ToString());

        var lineNodes = (await _db.Nodes.AsNoTracking()
                .Where(n => n.HostType == "LineNode" && n.LineId != null)
                .Select(n => new { n.Id, n.Name, n.LineId })
                .ToListAsync(ct))
            .Where(n => lineSpaceMap.ContainsKey(n.LineId!))
            .Select(n => new LineNodeRow(
                n.Id, n.Name ?? "", n.LineId!, lineSpaceMap[n.LineId!],
                terminalRoleMap.GetValueOrDefault((n.LineId!, n.Id)),
                terminalStateMap.GetValueOrDefault((n.LineId!, n.Id))))
            .ToList();

        var connectsTo = terminalInfo
            .Where(t => t.ExternalNodeId != null)
            .Select(t => (t.NodeId, t.ExternalNodeId!, t.Role.ToString(), t.OperatingState.ToString()))
            .ToList();

        var semanticEdges = await _db.Edges.AsNoTracking()
            .Select(e => new SemanticEdgeRow(e.SpaceId, e.SrcVid, e.DstVid, e.EdgeType, e.Rank, e.Props))
            .ToListAsync(ct);

        return new GraphContext(
            nodeNames, spaceTrees, unitTrees, categoryTrees, knowledgeTrees,
            freeNodes, lines, lineNodes, connectsTo, semanticEdges);
    }

    /// <summary>单空间图批次（顶点 + 边，含分类计数）。</summary>
    private sealed record SpaceBatch(
        List<VertexItem> Vertices, List<EdgeItem> Edges,
        int ParentEdges, int LineEdges, int SemanticEdges);

    /// <summary>
    /// 按路由规则构建单空间批次：
    /// 空间树→根对应该空间的节点；单位/分类/知识树→对应种子空间；自由 Node→全部空间；
    /// 线路/线路节点/connects_to→线路所属空间；语义边→edges.space_id 本空间。
    /// </summary>
    private SpaceBatch BuildSpaceBatch(
        GraphContext ctx, string spaceId,
        bool includeSpaceTree = false, bool includeTrees = false, bool includeFreeNodes = false,
        bool includeLines = false, bool includeSemantic = false)
    {
        var vertices = new List<VertexItem>();
        var edges = new List<EdgeItem>();
        var parentEdges = 0;
        var lineEdges = 0;
        var semanticEdges = 0;

        string NameOf(string id) => ctx.NodeNames.GetValueOrDefault(id) ?? "";

        void AddTree(IEnumerable<TreeRow> rows, string tag, string nodeType)
        {
            foreach (var n in rows)
            {
                vertices.Add(MakeVertex(n.Id, NameOf(n.Id), tag, nodeType, n.IsArchived));
                if (n.ParentId is not null)
                {
                    edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
                    parentEdges++;
                }
            }
        }

        if (includeSpaceTree)
            AddTree(ctx.SpaceTrees.Where(r => r.RootSpaceId == spaceId)
                    .Select(r => new TreeRow(r.Id, r.ParentId, false)),
                GraphNaming.SpaceTreeTag, "SpaceTree");

        if (includeTrees)
        {
            if (spaceId == SeedSpaceIds.Unit)
                AddTree(ctx.UnitTrees, GraphNaming.UnitTreeTag, "UnitTree");
            if (spaceId == SeedSpaceIds.Type)
                AddTree(ctx.CategoryTrees, GraphNaming.CategoryTreeTag, "CategoryTree");
            if (spaceId == SeedSpaceIds.Knowledge)
                AddTree(ctx.KnowledgeTrees, GraphNaming.KnowledgeTreeTag, "KnowledgeTree");
        }

        if (includeFreeNodes)
            foreach (var n in ctx.FreeNodes)
                vertices.Add(MakeVertex(n.Id, n.Name ?? "", GraphNaming.NodeTag, "Node", false));

        if (includeLines)
        {
            var edgeStart = edges.Count;

            foreach (var line in ctx.Lines.Where(l => l.SpaceId == spaceId))
                vertices.Add(MakeVertex(line.Id, line.Name, GraphNaming.LineTag, "Line", false));

            foreach (var n in ctx.LineNodes.Where(n => n.LineSpaceId == spaceId))
            {
                vertices.Add(MakeVertex(n.Id, n.Name, GraphNaming.LineNodeTag, "LineNode", false,
                    new Dictionary<string, object?>
                    {
                        ["line_id"] = n.LineId,
                        ["line_name"] = NameOf(n.LineId),
                        ["terminal_role"] = n.TerminalRole,
                        ["terminal_operating_state"] = n.TerminalState
                    }));
                // R7：Line → LineNode 归属边复用 parent_of
                edges.Add(new EdgeItem(n.LineId, n.Id, GraphNaming.ParentOfEdge, 0, null));
            }

            // connects_to 跟随 LineNode 所在线路空间
            var lineNodeIds = ctx.LineNodes
                .Where(n => n.LineSpaceId == spaceId)
                .Select(n => n.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var t in ctx.ConnectsTo.Where(t => lineNodeIds.Contains(t.NodeId)))
            {
                edges.Add(new EdgeItem(t.NodeId, t.ExternalNodeId, GraphNaming.ConnectsToEdge, 0,
                    new Dictionary<string, object?>
                    {
                        ["role"] = t.Role,
                        ["operating_state"] = t.OperatingState
                    }));
            }

            lineEdges = edges.Count - edgeStart;
        }

        if (includeSemantic)
        {
            foreach (var e in ctx.SemanticEdges.Where(e => e.SpaceId == spaceId))
            {
                edges.Add(new EdgeItem(e.SrcVid, e.DstVid, e.EdgeType, e.Rank, PropsToDict(e.Props)));
                semanticEdges++;
            }
        }

        return new SpaceBatch(vertices, edges, parentEdges, lineEdges, semanticEdges);
    }

    private static VertexItem MakeVertex(
        string id, string name, string tag, string nodeType, bool isArchived) =>
        MakeVertex(id, name, tag, nodeType, isArchived, null);

    private static VertexItem MakeVertex(
        string id, string name, string tag, string nodeType, bool isArchived,
        IReadOnlyDictionary<string, object?>? extra)
    {
        var props = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["node_type"] = nodeType,
            ["is_archived"] = isArchived
        };
        if (extra is not null)
            foreach (var (k, v) in extra)
                props[k] = v;
        return new(id, [tag], props);
    }
}

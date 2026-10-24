using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TreeGraph.Api.Data;
using TreeGraph.Shared.Messaging;

namespace TreeGraph.Api.GraphSyncSky;

/// <summary>图内节点快照（跨 4 棵宿主树 + LineNode/Line 解析结果）。</summary>
public sealed record GraphNodeInfo(
    string Id, string Name, string Tag, string NodeType, bool IsArchived, string? ParentId,
    IReadOnlyDictionary<string, object?>? ExtraProps = null);

/// <summary>全量重建结果统计。</summary>
public sealed record GraphRebuildResult(
    int Vertices, int ParentEdges, int LineEdges, int SemanticEdges,
    int VerticesWritten, int EdgesWritten, IReadOnlyList<string> Errors);

/// <summary>
/// 图同步器：统一 consumer / 全量重建端点的同步逻辑。
/// 消息只带 Id，状态从 PostgreSQL 现取（读最新已提交状态），同步幂等。
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
            await _client.DeleteVertexAsync(inodeId, ct);
            _logger.LogInformation("图顶点已删除：{InodeId}", inodeId);
            return false;
        }

        await _client.UpsertVertexAsync(node.Id, node.Tag, VertexProps(node), ct);

        await SyncParentEdgeAsync(node, ct);

        // 线路节点关联：connects_to（node → 外部 inode，属性 role；无端子/无外部连接时清空）。
        // R7：Line→LineNode 归属走 parent_of（SyncParentEdgeAsync），原归属边类型已下线；
        //     line_segment 段边即 relation 值，走 RelationGraphSyncConsumer 管道。
        if (node.Tag == GraphNaming.LineNodeTag)
            await SyncLineNodeLinksAsync(node, ct);

        _logger.LogDebug("图顶点已同步：{InodeId} tag={Tag}", node.Id, node.Tag);
        return true;
    }

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

    /// <summary>对齐 parent_of 边：删除不再是当前父的残留边，补上缺失的当前父边。</summary>
    private async Task SyncParentEdgeAsync(GraphNodeInfo node, CancellationToken ct)
    {
        var result = await _client.QueryAsync(
            $"GO FROM \"{node.Id}\" OVER `{GraphNaming.ParentOfEdge}` REVERSELY " +
            "YIELD src(edge) AS parent", ct);

        var existing = result.Data?
            .Select(r => r.TryGetValue("parent", out var v) ? v.GetString() : null)
            .Where(s => !string.IsNullOrEmpty(s))
            .Cast<string>()
            .ToList() ?? [];

        foreach (var parent in existing.Where(p => p != node.ParentId))
            await _client.DeleteEdgeAsync(GraphNaming.ParentOfEdge, parent, node.Id, ct);

        if (node.ParentId is not null && !existing.Contains(node.ParentId))
            await _client.UpsertEdgeAsync(GraphNaming.ParentOfEdge, node.ParentId, node.Id, null, ct);
    }

    /// <summary>
    /// 线路节点关联边：connects_to（node → 外部 inode，属性 role；无端子/无外部连接时清空）。
    /// R7：原归属边类型的对齐段已删除（归属走 parent_of）。
    /// </summary>
    private async Task SyncLineNodeLinksAsync(GraphNodeInfo node, CancellationToken ct)
    {
        // 外部连接：端子表为准（一个节点同属一条线路，故至多一条端子）
        var terminal = await _db.LineTerminals.AsNoTracking()
            .Where(t => t.NodeId == node.Id)
            .Select(t => new { t.Role, t.OperatingState, t.ExternalNodeId })
            .FirstOrDefaultAsync(ct);

        var connects = await _client.QueryAsync(
            $"GO FROM \"{node.Id}\" OVER `{GraphNaming.ConnectsToEdge}` " +
            "YIELD dst(edge) AS target", ct);
        var existingTargets = QueryColumn(connects, "target");

        foreach (var old in existingTargets.Where(x => x != terminal?.ExternalNodeId))
            await _client.DeleteEdgeAsync(GraphNaming.ConnectsToEdge, node.Id, old, ct);

        if (terminal?.ExternalNodeId is not null
            && !existingTargets.Contains(terminal.ExternalNodeId))
        {
            await _client.UpsertEdgeAsync(
                GraphNaming.ConnectsToEdge, node.Id, terminal.ExternalNodeId,
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
    /// 不再逐一试各个宿主守卫。
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
                return new GraphNodeInfo(inodeId, row.Name, GraphNaming.SpaceTreeTag,
                    "SpaceTree", false, parentId);
            }
            case "UnitTree":
            {
                var parentId = await _db.UnitTrees.AsNoTracking()
                    .Where(n => n.Id == inodeId)
                    .Select(n => n.ParentId)
                    .FirstOrDefaultAsync(ct);
                return new GraphNodeInfo(inodeId, row.Name, GraphNaming.UnitTreeTag,
                    "UnitTree", false, parentId);
            }
            case "CategoryTree":
            {
                var h = await _db.CategoryTrees.AsNoTracking()
                    .Where(n => n.Id == inodeId)
                    .Select(n => new { n.ParentId, n.IsArchived })
                    .FirstOrDefaultAsync(ct);
                return new GraphNodeInfo(inodeId, row.Name, GraphNaming.CategoryTreeTag,
                    "CategoryTree", h?.IsArchived ?? false, h?.ParentId);
            }
            case "KnowledgeTree":
            {
                var h = await _db.KnowledgeTrees.AsNoTracking()
                    .Where(n => n.Id == inodeId)
                    .Select(n => new { n.ParentId, n.IsArchived })
                    .FirstOrDefaultAsync(ct);
                return new GraphNodeInfo(inodeId, row.Name, GraphNaming.KnowledgeTreeTag,
                    "KnowledgeTree", h?.IsArchived ?? false, h?.ParentId);
            }
            case "LineNode":
            {
                // R7：line_id 即 nodes.line_id（身份锚点）；parent_of 边指向所属 Line
                var lineId = row.LineId;
                if (lineId is null) return null;

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
                    });
            }
            case "Line":
                // R7：Line 注册在 nodes，顶点链路直连；拓扑模式已下线（结构由边连接定义）
                return new GraphNodeInfo(inodeId, row.Name, GraphNaming.LineTag,
                    "Line", false, null);
            case "Node":
                // 自由节点：无宿主扩展表，只入身份与名称
                return new GraphNodeInfo(inodeId, row.Name, GraphNaming.NodeTag,
                    "Node", false, null);

            default:
                // 其它宿主身份暂不入图（R4 不做超边/边顶点）
                return null;
        }
    }

    // ============================================================
    // 关系边（relation_attribute_values）
    // ============================================================

    /// <summary>
    /// 同步单条关系边。Delete 时 PG 行已不存在，依赖事件载荷定位图边；
    /// upsert 同样以事件载荷为准（事件在 SaveChanges 成功后发布，载荷即最新状态）。
    /// 图边属性：value_id / attribute_id / name。
    /// </summary>
    public async Task SyncRelationAsync(RelationChangedEvent evt, CancellationToken ct = default)
    {
        if (evt.Kind == RelationChangeKind.Delete)
        {
            await _client.DeleteEdgeAsync(evt.RelationType, evt.FromNodeId, evt.ToNodeId, ct);
            _logger.LogInformation("图边已删除：{ValueId} type={Type}", evt.ValueId, evt.RelationType);
            return;
        }

        await _client.UpsertEdgeAsync(evt.RelationType, evt.FromNodeId, evt.ToNodeId,
            new Dictionary<string, object?>
            {
                ["value_id"] = evt.ValueId,
                ["attribute_id"] = evt.AttributeId,
                ["name"] = evt.Name
            }, ct);
        _logger.LogDebug("图边已同步：{ValueId} type={Type}", evt.ValueId, evt.RelationType);
    }

    // ============================================================
    // 全量重建
    // ============================================================

    /// <summary>
    /// 全量重建：DROP SPACE → 重建 schema → 从 PG 全量装载顶点/边批量写入。
    /// 写失败批次计入 Errors，不中断整体流程。
    /// </summary>
    public async Task<GraphRebuildResult> RebuildAllAsync(CancellationToken ct)
    {
        await _client.QueryAsync($"DROP SPACE IF EXISTS `{_options.Space}`", ct);
        await _schemaInitializer.RecreateSchemaAsync(ct);

        var vertices = new List<VertexItem>();
        var edges = new List<EdgeItem>();

        // ── 顶点 + parent_of 树父子边 ──（名称统一来自 nodes 注册表）
        var spaceNames = await LoadNodeNamesAsync("SpaceTree", ct);
        foreach (var n in await _db.SpaceTrees.AsNoTracking()
                     .Select(n => new { n.Id, n.ParentId }).ToListAsync(ct))
        {
            vertices.Add(MakeVertex(n.Id, spaceNames.GetValueOrDefault(n.Id) ?? "", GraphNaming.SpaceTreeTag, "SpaceTree", false));
            if (n.ParentId is not null)
                edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
        }

        var unitNames = await LoadNodeNamesAsync("UnitTree", ct);
        foreach (var n in await _db.UnitTrees.AsNoTracking()
                     .Select(n => new { n.Id, n.ParentId }).ToListAsync(ct))
        {
            vertices.Add(MakeVertex(n.Id, unitNames.GetValueOrDefault(n.Id) ?? "", GraphNaming.UnitTreeTag, "UnitTree", false));
            if (n.ParentId is not null)
                edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
        }

        var categoryNames = await LoadNodeNamesAsync("CategoryTree", ct);
        foreach (var n in await _db.CategoryTrees.AsNoTracking()
                     .Select(n => new { n.Id, n.ParentId, n.IsArchived }).ToListAsync(ct))
        {
            vertices.Add(MakeVertex(n.Id, categoryNames.GetValueOrDefault(n.Id) ?? "", GraphNaming.CategoryTreeTag, "CategoryTree", n.IsArchived));
            if (n.ParentId is not null)
                edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
        }

        var knowledgeNames = await LoadNodeNamesAsync("KnowledgeTree", ct);
        foreach (var n in await _db.KnowledgeTrees.AsNoTracking()
                     .Select(n => new { n.Id, n.ParentId, n.IsArchived }).ToListAsync(ct))
        {
            vertices.Add(MakeVertex(n.Id, knowledgeNames.GetValueOrDefault(n.Id) ?? "", GraphNaming.KnowledgeTreeTag, "KnowledgeTree", n.IsArchived));
            if (n.ParentId is not null)
                edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
        }

        // ── 自由节点（nodes.host_type = "Node"）──
        foreach (var n in await _db.Nodes.AsNoTracking()
                     .Where(x => x.HostType == "Node")
                     .Select(x => new { x.Id, x.Name }).ToListAsync(ct))
        {
            vertices.Add(MakeVertex(n.Id, n.Name ?? "", GraphNaming.NodeTag, "Node", false));
        }

        var parentEdgeCount = edges.Count;

        // ── 线路顶点/节点 + parent_of（Line→LineNode）/ connects_to 边 ──
        var lineEdgeCount = await AddLineGraphAsync(vertices, edges, ct);

        // ── 关系边（relation_attribute_values）──
        var semanticEdges = await _db.RelationAttributeValues.AsNoTracking()
            .Select(r => new { FromNodeId = r.EntityId, r.ToNodeId, r.RelationType, r.ValueId, r.AttributeId, r.Name })
            .ToListAsync(ct);

        foreach (var e in semanticEdges)
        {
            edges.Add(new EdgeItem(e.FromNodeId, e.ToNodeId, e.RelationType, 0,
                new Dictionary<string, object?>
                {
                    ["value_id"] = e.ValueId,
                    ["attribute_id"] = e.AttributeId,
                    ["name"] = e.Name
                }));
        }

        // ── 批量写入 ──
        // DROP SPACE 后重建 schema，storaged 侧生效滞后于 metad（官方建议等两个心跳周期），
        // 立即写入会报 No schema found/EdgeNotFound；bulk 为幂等覆盖写，整体重试等待传播。
        var result = await BulkWithSchemaRetryAsync(vertices, edges, ct);

        var errors = new List<string>();
        foreach (var err in result.VerticesErrors ?? [])
            errors.Add($"vertices: {err.GetValueOrDefault("error")}");
        foreach (var err in result.EdgesErrors ?? [])
            errors.Add($"edges: {err.GetValueOrDefault("error")}");

        _logger.LogInformation(
            "图全量重建完成：顶点 {V}/{VT}，边 {E}/{ET}，错误批次 {Err}",
            result.VerticesSuccess, result.VerticesTotal,
            result.EdgesSuccess, result.EdgesTotal, errors.Count);

        return new GraphRebuildResult(
            vertices.Count, parentEdgeCount, lineEdgeCount, semanticEdges.Count,
            result.VerticesSuccess, result.EdgesSuccess, errors);
    }

    /// <summary>
    /// 按类别同步（同步刷新对话框用）：把 PG 中该类别现状批量覆盖写入 NebulaGraph。
    /// 幂等覆盖写，不 DROP SPACE；图内残留清理仍靠事件同步 / 全量重建。
    /// </summary>
    public async Task<GraphRebuildResult> SyncCategoryAsync(SyncCategory category, CancellationToken ct)
    {
        var vertices = new List<VertexItem>();
        var edges = new List<EdgeItem>();
        var parentEdgeCount = 0;
        var lineEdgeCount = 0;
        var semanticEdgeCount = 0;

        switch (category)
        {
            case SyncCategory.Space:
            {
                var names = await LoadNodeNamesAsync("SpaceTree", ct);
                foreach (var n in await _db.SpaceTrees.AsNoTracking()
                             .Select(n => new { n.Id, n.ParentId }).ToListAsync(ct))
                {
                    vertices.Add(MakeVertex(n.Id, names.GetValueOrDefault(n.Id) ?? "", GraphNaming.SpaceTreeTag, "SpaceTree", false));
                    if (n.ParentId is not null)
                        edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
                }
                parentEdgeCount = edges.Count;
                break;
            }

            case SyncCategory.Tree:
            {
                var unitNames = await LoadNodeNamesAsync("UnitTree", ct);
                foreach (var n in await _db.UnitTrees.AsNoTracking()
                             .Select(n => new { n.Id, n.ParentId }).ToListAsync(ct))
                {
                    vertices.Add(MakeVertex(n.Id, unitNames.GetValueOrDefault(n.Id) ?? "", GraphNaming.UnitTreeTag, "UnitTree", false));
                    if (n.ParentId is not null)
                        edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
                }

                var categoryNames = await LoadNodeNamesAsync("CategoryTree", ct);
                foreach (var n in await _db.CategoryTrees.AsNoTracking()
                             .Select(n => new { n.Id, n.ParentId, n.IsArchived }).ToListAsync(ct))
                {
                    vertices.Add(MakeVertex(n.Id, categoryNames.GetValueOrDefault(n.Id) ?? "", GraphNaming.CategoryTreeTag, "CategoryTree", n.IsArchived));
                    if (n.ParentId is not null)
                        edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
                }

                var knowledgeNames = await LoadNodeNamesAsync("KnowledgeTree", ct);
                foreach (var n in await _db.KnowledgeTrees.AsNoTracking()
                             .Select(n => new { n.Id, n.ParentId, n.IsArchived }).ToListAsync(ct))
                {
                    vertices.Add(MakeVertex(n.Id, knowledgeNames.GetValueOrDefault(n.Id) ?? "", GraphNaming.KnowledgeTreeTag, "KnowledgeTree", n.IsArchived));
                    if (n.ParentId is not null)
                        edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
                }
                parentEdgeCount = edges.Count;
                break;
            }

            case SyncCategory.Line:
                lineEdgeCount = await AddLineGraphAsync(vertices, edges, ct);
                break;

            case SyncCategory.Edge:
                var semanticEdges = await _db.RelationAttributeValues.AsNoTracking()
                    .Select(r => new { FromNodeId = r.EntityId, r.ToNodeId, r.RelationType, r.ValueId, r.AttributeId, r.Name })
                    .ToListAsync(ct);

                semanticEdgeCount = semanticEdges.Count;
                foreach (var e in semanticEdges)
                {
                    edges.Add(new EdgeItem(e.FromNodeId, e.ToNodeId, e.RelationType, 0,
                        new Dictionary<string, object?>
                        {
                            ["value_id"] = e.ValueId,
                            ["attribute_id"] = e.AttributeId,
                            ["name"] = e.Name
                        }));
                }
                break;
        }

        // 不 DROP，schema 恒在；BulkWithSchemaRetryAsync 仅在出现传播类瞬时错误时整体重试
        var result = await BulkWithSchemaRetryAsync(vertices, edges, ct);

        var errors = new List<string>();
        foreach (var err in result.VerticesErrors ?? [])
            errors.Add($"vertices: {err.GetValueOrDefault("error")}");
        foreach (var err in result.EdgesErrors ?? [])
            errors.Add($"edges: {err.GetValueOrDefault("error")}");

        _logger.LogInformation(
            "图按类别同步（{Category}）：顶点 {V}/{VT}，边 {E}/{ET}，错误批次 {Err}",
            category, result.VerticesSuccess, result.VerticesTotal,
            result.EdgesSuccess, result.EdgesTotal, errors.Count);

        return new GraphRebuildResult(
            vertices.Count, parentEdgeCount, lineEdgeCount, semanticEdgeCount,
            result.VerticesSuccess, result.EdgesSuccess, errors);
    }

    private async Task<BulkResult> BulkWithSchemaRetryAsync(
        List<VertexItem> vertices, List<EdgeItem> edges, CancellationToken ct)
    {
        const int maxAttempts = 12; // 首次 + 11 次 × 5s ≈ 覆盖 55s 传播窗口
        for (var attempt = 1; ; attempt++)
        {
            var result = await _client.BulkAsync(vertices, edges, _options.RebuildBatchSize, ct);
            var allErrors = (result.VerticesErrors ?? []).Concat(result.EdgesErrors ?? [])
                .Select(e => e.TryGetValue("error", out var v) ? v.GetString() ?? "" : "")
                .ToList();

            if (allErrors.Count == 0 || !allErrors.Any(IsRetryableBulkError) || attempt >= maxAttempts)
                return result;

            _logger.LogWarning(
                "全量重建遇到瞬时错误（schema 传播延迟 / storage 重选主，第 {Attempt}/{Max} 次），5s 后整体重试",
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

    /// <summary>
    /// 装载线路图：Line 顶点、LineNode 顶点（所属线路与端子角色）、
    /// parent_of（Line → LineNode，R7 归属边）与 connects_to（LineNode → 外部 inode，带 role）。
    /// R7：line_segment 段边即 relation 值，由语义边段统一装载（计入 SemanticEdges）。
    /// 返回追加的边数。
    /// </summary>
    private async Task<int> AddLineGraphAsync(
        List<VertexItem> vertices, List<EdgeItem> edges, CancellationToken ct)
    {
        var edgeStart = edges.Count;

        // 名称唯一真源在 nodes（R2 收编）；Line/LineNode 均注册于 nodes
        var lineNames = await LoadNodeNamesAsync("Line", ct);
        var terminalInfo = await _db.LineTerminals.AsNoTracking()
            .Select(t => new { t.LineId, t.NodeId, t.Role, t.OperatingState })
            .ToListAsync(ct);
        var terminalRoleMap = terminalInfo.ToDictionary(
            x => (x.LineId, x.NodeId),
            x => x.Role.ToString());
        var terminalStateMap = terminalInfo.ToDictionary(
            x => (x.LineId, x.NodeId),
            x => x.OperatingState.ToString());

        foreach (var (lineId, name) in lineNames)
        {
            vertices.Add(MakeVertex(lineId, name ?? "", GraphNaming.LineTag, "Line", false));
        }

        var lineNodeRows = await _db.Nodes.AsNoTracking()
            .Where(n => n.HostType == "LineNode" && n.LineId != null)
            .Select(n => new { n.Id, n.Name, n.LineId })
            .ToListAsync(ct);

        foreach (var n in lineNodeRows)
        {
            vertices.Add(MakeVertex(n.Id, n.Name ?? "", GraphNaming.LineNodeTag, "LineNode", false,
                new Dictionary<string, object?>
                {
                    ["line_id"] = n.LineId,
                    ["line_name"] = lineNames.GetValueOrDefault(n.LineId!) ?? string.Empty,
                    ["terminal_role"] = terminalRoleMap.GetValueOrDefault((n.LineId!, n.Id)),
                    ["terminal_operating_state"] = terminalStateMap.GetValueOrDefault((n.LineId!, n.Id))
                }));
            // R7：Line → LineNode 归属边复用 parent_of
            edges.Add(new EdgeItem(n.LineId!, n.Id, GraphNaming.ParentOfEdge, 0, null));
        }

        var terminals = await _db.LineTerminals.AsNoTracking()
            .Where(t => t.ExternalNodeId != null)
            .Select(t => new { t.NodeId, t.ExternalNodeId, t.Role, t.OperatingState })
            .ToListAsync(ct);
        foreach (var t in terminals)
        {
            edges.Add(new EdgeItem(t.NodeId, t.ExternalNodeId!, GraphNaming.ConnectsToEdge, 0,
                new Dictionary<string, object?>
                {
                    ["role"] = t.Role.ToString(),
                    ["operating_state"] = t.OperatingState.ToString()
                }));
        }

        return edges.Count - edgeStart;
    }

    /// <summary>按 host_type 从 nodes 注册表批量取名称（单表查询）。</summary>
    private async Task<Dictionary<string, string?>> LoadNodeNamesAsync(
        string hostType, CancellationToken ct)
        => await _db.Nodes.AsNoTracking()
            .Where(n => n.HostType == hostType)
            .ToDictionaryAsync(n => n.Id, n => n.Name, ct);

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

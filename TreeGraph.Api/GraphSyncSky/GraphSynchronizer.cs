using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TreeGraph.Api.Data;
using TreeGraph.Shared.Messaging;

namespace TreeGraph.Api.GraphSyncSky;

/// <summary>图内节点快照（跨 4 棵宿主树 + LineNode 解析结果）。</summary>
public sealed record GraphNodeInfo(
    string Id, string Name, string Tag, string NodeType, bool IsArchived, string? ParentId);

/// <summary>全量重建结果统计。</summary>
public sealed record GraphRebuildResult(
    int Vertices, int ParentEdges, int SemanticEdges,
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

        await _client.UpsertVertexAsync(node.Id, node.Tag, new Dictionary<string, object?>
        {
            ["name"] = node.Name,
            ["node_type"] = node.NodeType,
            ["is_archived"] = node.IsArchived
        }, ct);

        await SyncParentEdgeAsync(node, ct);

        _logger.LogDebug("图顶点已同步：{InodeId} tag={Tag}", node.Id, node.Tag);
        return true;
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

    /// <summary>跨 4 棵宿主树 + LineNode 解析节点（Id 全局唯一，先中先得）。</summary>
    private async Task<GraphNodeInfo?> ResolveNodeAsync(string inodeId, CancellationToken ct)
    {
        var space = await _db.SpaceTrees.AsNoTracking()
            .Where(n => n.Id == inodeId)
            .Select(n => new { n.Name, n.ParentId })
            .FirstOrDefaultAsync(ct);
        if (space is not null)
            return new GraphNodeInfo(inodeId, space.Name, GraphNaming.SpaceTreeTag,
                "SpaceTree", false, space.ParentId);

        var unit = await _db.UnitTrees.AsNoTracking()
            .Where(n => n.Id == inodeId)
            .Select(n => new { n.Name, n.ParentId })
            .FirstOrDefaultAsync(ct);
        if (unit is not null)
            return new GraphNodeInfo(inodeId, unit.Name, GraphNaming.UnitTreeTag,
                "UnitTree", false, unit.ParentId);

        var category = await _db.CategoryTrees.AsNoTracking()
            .Where(n => n.Id == inodeId)
            .Select(n => new { n.Name, n.ParentId, n.IsArchived })
            .FirstOrDefaultAsync(ct);
        if (category is not null)
            return new GraphNodeInfo(inodeId, category.Name, GraphNaming.CategoryTreeTag,
                "CategoryTree", category.IsArchived, category.ParentId);

        var knowledge = await _db.KnowledgeTrees.AsNoTracking()
            .Where(n => n.Id == inodeId)
            .Select(n => new { n.Name, n.ParentId, n.IsArchived })
            .FirstOrDefaultAsync(ct);
        if (knowledge is not null)
            return new GraphNodeInfo(inodeId, knowledge.Name, GraphNaming.KnowledgeTreeTag,
                "KnowledgeTree", knowledge.IsArchived, knowledge.ParentId);

        var line = await _db.LineNodes.AsNoTracking()
            .Where(n => n.Id == inodeId)
            .Select(n => new { n.Name })
            .FirstOrDefaultAsync(ct);
        if (line is not null)
            return new GraphNodeInfo(inodeId, line.Name, GraphNaming.LineNodeTag,
                "LineNode", false, null);

        return null;
    }

    // ============================================================
    // 语义边（EdgeInstance）
    // ============================================================

    /// <summary>
    /// 同步单条语义边。Deleted 时 PG 行已不存在，依赖事件载荷（EdgeTypeName/From/To）定位图边。
    /// </summary>
    public async Task SyncEdgeAsync(EdgeChangedEvent evt, CancellationToken ct = default)
    {
        if (evt.Kind == EdgeChangeKind.Deleted)
        {
            await DeleteEdgeByPayloadAsync(evt, ct);
            return;
        }

        var inst = await _db.EdgeInstances.AsNoTracking()
            .FirstOrDefaultAsync(i => i.EdgeId == evt.EdgeId, ct);
        if (inst is null)
        {
            // 行已删但事件为 Created/Updated（乱序）——按载荷删除对齐
            _logger.LogInformation("边 {EdgeId} 在 PG 不存在，按载荷删除对齐", evt.EdgeId);
            await DeleteEdgeByPayloadAsync(evt, ct);
            return;
        }

        var def = await _db.EdgeDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(d => d.EdgeTypeId == inst.EdgeTypeId, ct);
        if (def is null)
        {
            _logger.LogWarning("边 {EdgeId} 的类型定义 {EdgeTypeId} 不存在，跳过", inst.EdgeId, inst.EdgeTypeId);
            return;
        }

        await _client.UpsertEdgeAsync(def.TypeName, inst.FromNodeId, inst.ToNodeId,
            EdgeProps(inst), ct);
        _logger.LogDebug("图边已同步：{EdgeId} type={Type}", inst.EdgeId, def.TypeName);
    }

    private async Task DeleteEdgeByPayloadAsync(EdgeChangedEvent evt, CancellationToken ct)
    {
        if (evt.EdgeTypeName is null || evt.FromNodeId is null || evt.ToNodeId is null)
        {
            _logger.LogWarning("边删除事件 {EdgeId} 缺少类型/端点载荷，无法定位图边", evt.EdgeId);
            return;
        }
        await _client.DeleteEdgeAsync(evt.EdgeTypeName, evt.FromNodeId, evt.ToNodeId, ct);
        _logger.LogInformation("图边已删除：{EdgeId} type={Type}", evt.EdgeId, evt.EdgeTypeName);
    }

    private static Dictionary<string, object?> EdgeProps(EdgeSky.Entities.EdgeInstance inst) => new()
    {
        ["edge_id"] = inst.EdgeId,
        ["weight"] = inst.Weight.HasValue ? (double)inst.Weight.Value : null,
        ["display_name"] = inst.DisplayName
    };

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

        // ── 顶点 + parent_of 树父子边 ──
        foreach (var n in await _db.SpaceTrees.AsNoTracking()
                     .Select(n => new { n.Id, n.Name, n.ParentId }).ToListAsync(ct))
        {
            vertices.Add(MakeVertex(n.Id, n.Name, GraphNaming.SpaceTreeTag, "SpaceTree", false));
            if (n.ParentId is not null)
                edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
        }

        foreach (var n in await _db.UnitTrees.AsNoTracking()
                     .Select(n => new { n.Id, n.Name, n.ParentId }).ToListAsync(ct))
        {
            vertices.Add(MakeVertex(n.Id, n.Name, GraphNaming.UnitTreeTag, "UnitTree", false));
            if (n.ParentId is not null)
                edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
        }

        foreach (var n in await _db.CategoryTrees.AsNoTracking()
                     .Select(n => new { n.Id, n.Name, n.ParentId, n.IsArchived }).ToListAsync(ct))
        {
            vertices.Add(MakeVertex(n.Id, n.Name, GraphNaming.CategoryTreeTag, "CategoryTree", n.IsArchived));
            if (n.ParentId is not null)
                edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
        }

        foreach (var n in await _db.KnowledgeTrees.AsNoTracking()
                     .Select(n => new { n.Id, n.Name, n.ParentId, n.IsArchived }).ToListAsync(ct))
        {
            vertices.Add(MakeVertex(n.Id, n.Name, GraphNaming.KnowledgeTreeTag, "KnowledgeTree", n.IsArchived));
            if (n.ParentId is not null)
                edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
        }

        foreach (var n in await _db.LineNodes.AsNoTracking()
                     .Select(n => new { n.Id, n.Name }).ToListAsync(ct))
        {
            vertices.Add(MakeVertex(n.Id, n.Name, GraphNaming.LineNodeTag, "LineNode", false));
        }

        var parentEdgeCount = edges.Count;

        // ── 语义边 ──
        var semanticEdges = await (
            from i in _db.EdgeInstances.AsNoTracking()
            join d in _db.EdgeDefinitions.AsNoTracking() on i.EdgeTypeId equals d.EdgeTypeId
            select new { i.FromNodeId, i.ToNodeId, d.TypeName, i.EdgeId, i.Weight, i.DisplayName }
        ).ToListAsync(ct);

        foreach (var e in semanticEdges)
        {
            edges.Add(new EdgeItem(e.FromNodeId, e.ToNodeId, e.TypeName, 0,
                new Dictionary<string, object?>
                {
                    ["edge_id"] = e.EdgeId,
                    ["weight"] = e.Weight.HasValue ? (double)e.Weight.Value : null,
                    ["display_name"] = e.DisplayName
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
            vertices.Count, parentEdgeCount, semanticEdges.Count,
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
        var semanticEdgeCount = 0;

        switch (category)
        {
            case SyncCategory.Space:
                foreach (var n in await _db.SpaceTrees.AsNoTracking()
                             .Select(n => new { n.Id, n.Name, n.ParentId }).ToListAsync(ct))
                {
                    vertices.Add(MakeVertex(n.Id, n.Name, GraphNaming.SpaceTreeTag, "SpaceTree", false));
                    if (n.ParentId is not null)
                        edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
                }
                parentEdgeCount = edges.Count;
                break;

            case SyncCategory.Tree:
                foreach (var n in await _db.UnitTrees.AsNoTracking()
                             .Select(n => new { n.Id, n.Name, n.ParentId }).ToListAsync(ct))
                {
                    vertices.Add(MakeVertex(n.Id, n.Name, GraphNaming.UnitTreeTag, "UnitTree", false));
                    if (n.ParentId is not null)
                        edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
                }

                foreach (var n in await _db.CategoryTrees.AsNoTracking()
                             .Select(n => new { n.Id, n.Name, n.ParentId, n.IsArchived }).ToListAsync(ct))
                {
                    vertices.Add(MakeVertex(n.Id, n.Name, GraphNaming.CategoryTreeTag, "CategoryTree", n.IsArchived));
                    if (n.ParentId is not null)
                        edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
                }

                foreach (var n in await _db.KnowledgeTrees.AsNoTracking()
                             .Select(n => new { n.Id, n.Name, n.ParentId, n.IsArchived }).ToListAsync(ct))
                {
                    vertices.Add(MakeVertex(n.Id, n.Name, GraphNaming.KnowledgeTreeTag, "KnowledgeTree", n.IsArchived));
                    if (n.ParentId is not null)
                        edges.Add(new EdgeItem(n.ParentId, n.Id, GraphNaming.ParentOfEdge, 0, null));
                }
                parentEdgeCount = edges.Count;
                break;

            case SyncCategory.Line:
                foreach (var n in await _db.LineNodes.AsNoTracking()
                             .Select(n => new { n.Id, n.Name }).ToListAsync(ct))
                {
                    vertices.Add(MakeVertex(n.Id, n.Name, GraphNaming.LineNodeTag, "LineNode", false));
                }
                break;

            case SyncCategory.Edge:
                var semanticEdges = await (
                    from i in _db.EdgeInstances.AsNoTracking()
                    join d in _db.EdgeDefinitions.AsNoTracking() on i.EdgeTypeId equals d.EdgeTypeId
                    select new { i.FromNodeId, i.ToNodeId, d.TypeName, i.EdgeId, i.Weight, i.DisplayName }
                ).ToListAsync(ct);

                semanticEdgeCount = semanticEdges.Count;
                foreach (var e in semanticEdges)
                {
                    edges.Add(new EdgeItem(e.FromNodeId, e.ToNodeId, e.TypeName, 0,
                        new Dictionary<string, object?>
                        {
                            ["edge_id"] = e.EdgeId,
                            ["weight"] = e.Weight.HasValue ? (double)e.Weight.Value : null,
                            ["display_name"] = e.DisplayName
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
            vertices.Count, parentEdgeCount, semanticEdgeCount,
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

            if (allErrors.Count == 0 || !allErrors.Any(IsSchemaPropagationError) || attempt >= maxAttempts)
                return result;

            _logger.LogWarning(
                "全量重建遇到 schema 传播延迟（第 {Attempt}/{Max} 次），5s 后整体重试",
                attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    /// <summary>
    /// 判断是否为 schema 传播延迟类错误（重试可恢复）。
    /// DROP SPACE 重建后 storaged 侧 space 信息（含 vid 类型）刷新滞后，
    /// 写入会报 vid 长度不符——同属传播延迟，须纳入重试。
    /// </summary>
    private static bool IsSchemaPropagationError(string msg) =>
        msg.Contains("No schema found") || msg.Contains("EdgeNotFound")
        || msg.Contains("TagNotFound") || msg.Contains("SpaceNotFound")
        || msg.Contains("Invalid space vid len");

    private static VertexItem MakeVertex(
        string id, string name, string tag, string nodeType, bool isArchived) =>
        new(id, [tag], new Dictionary<string, object?>
        {
            ["name"] = name,
            ["node_type"] = nodeType,
            ["is_archived"] = isArchived
        });
}

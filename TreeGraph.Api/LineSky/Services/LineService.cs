using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.EdgeSky.Services;
using TreeGraph.Shared.LineSky.Dtos;
using TreeGraph.Shared.EdgeSky.Dtos;

namespace TreeGraph.Api.LineSky.Services;

/// <summary>
/// 线路结构（Line）专用服务。
///
/// 线路结构 = EdgeDefinition.IsLine = true 的一组边实例，
/// 语义上表达"点线相连的线路结构"（如输电线路、物流线路、工序链）。
///
/// 核心查询使用 PostgreSQL WITH RECURSIVE CTE（单次 SQL 拿完整路径）。
/// 拓扑分析内核（Kahn）复用 EdgeSky 的 <see cref="EdgeInstanceService.AnalyzeTopology"/>。
/// </summary>
public class LineService
{
    /// <summary>路径递归的最大展开层数；触顶即视为截断（返回 Truncated=true）。</summary>
    public const int MaxPathDepth = 500;

    private readonly TreeGraphDbContext _db;
    private readonly NodeEavSky.Services.InodeOwnerGuardRegistry _inodeGuards;

    public LineService(
        TreeGraphDbContext db,
        NodeEavSky.Services.InodeOwnerGuardRegistry inodeGuards)
    {
        _db = db;
        _inodeGuards = inodeGuards;
    }

    /// <summary>读取边类型定义（跟踪查询，便于变更链路复用同一实例）。</summary>
    public Task<EdgeDefinition?> GetDefinitionAsync(string edgeTypeId, CancellationToken ct)
        => _db.EdgeDefinitions.FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);

    // ============================================================
    // ① 批量创建线路（已移除）
    // ============================================================
    // 线路结构统一走 LineStructureService（api/eav/lines/{lineId}/structure），
    // 由 line_structures / line_structure_segments 表达头表与段归属。

    // ============================================================
    // ② 完整路径查询（WITH RECURSIVE CTE）
    // ============================================================

    /// <summary>
    /// 从 startNodeId 沿指定方向走到底，返回完整线路。
    /// direction=down → 沿 FromNodeId→ToNodeId 往下游走
    /// direction=up   → 沿 ToNodeId→FromNodeId 往上游走
    /// 无向线路（IsDirected=false）忽略 direction，统一按双向展开（up/down 结果一致）。
    /// 单次 SQL（WITH RECURSIVE），线性 O(N)；带 visited 去重防环，深度上限 MaxPathDepth。
    /// </summary>
    public async Task<LinePathResult> GetPathAsync(
        string edgeTypeId, string startNodeId, string direction,
        CancellationToken ct, int maxDepth = MaxPathDepth, EdgeDefinition? def = null)
    {
        // 调用方（如 LineController 的守卫）已加载定义时直接复用，避免重复查询。
        def ??= await _db.EdgeDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);

        // 无向线路（IsDirected=false）：up/down 语义不适用，统一按双向展开。
        var sql = def is { IsDirected: false }
            ? UndirectedCteSql
            : direction == "up" ? UpstreamCteSql : DownstreamCteSql;

        var edges = await _db.Database
            .SqlQueryRaw<LineEdgeRow>(sql,
                new Npgsql.NpgsqlParameter("edgeTypeId", edgeTypeId),
                new Npgsql.NpgsqlParameter("startNodeId", startNodeId),
                new Npgsql.NpgsqlParameter("maxDepth", maxDepth))
            .ToListAsync(ct);

        // 组装结果
        var orderedEdges = edges.OrderBy(e => e.Depth).ToList();
        // 触顶（存在深度 = maxDepth-1 的行）即视为截断，显式告知调用方而非静默返回不完整路径。
        var truncated = orderedEdges.Count > 0 && orderedEdges[^1].Depth >= maxDepth - 1;

        var nodeIds = new List<string> { startNodeId };
        var dtos = new List<EdgeInstanceDto>(orderedEdges.Count);

        // 两端 Inode 显示名：整批一次解析
        var names = await _inodeGuards.ResolveNodeNamesAsync(
            orderedEdges.SelectMany(r => new[] { r.FromNode, r.ToNode }), ct);

        foreach (var row in orderedEdges)
        {
            nodeIds.Add(row.NextNode);
            dtos.Add(new EdgeInstanceDto(
                EdgeId: row.EdgeId,
                EdgeTypeId: edgeTypeId,
                // def（调用方传入或上方兜底加载）必含 TypeName/DisplayName，直接复用，不再二次查询
                EdgeTypeName: def?.TypeName ?? "",
                EdgeTypeDisplayName: def?.DisplayName ?? "",
                FromNodeId: row.FromNode,
                FromNodeText: names.GetValueOrDefault(row.FromNode, ""),
                ToNodeId: row.ToNode,
                ToNodeText: names.GetValueOrDefault(row.ToNode, ""),
                Weight: row.Weight,
                DisplayName: null,
                CreatedAt: row.CreatedAt,
                UpdatedAt: row.UpdatedAt,
                Properties: null));
        }

        return new LinePathResult(nodeIds, dtos, dtos.Count, truncated);
    }

    /// <summary>向上游走：从当前节点沿 ToNode→FromNode 反向走（visited 去重防环）。</summary>
    private const string UpstreamCteSql = """
        WITH RECURSIVE line AS (
            SELECT edge_id, from_node_id, to_node_id, weight, created_at, updated_at,
                   from_node_id AS next_node, 0 AS depth, ARRAY[from_node_id::text] AS visited
            FROM edge_instances
            WHERE edge_type_id = @edgeTypeId AND to_node_id = @startNodeId

            UNION ALL

            SELECT e.edge_id, e.from_node_id, e.to_node_id, e.weight, e.created_at, e.updated_at,
                   e.from_node_id, c.depth + 1, c.visited || e.from_node_id::text
            FROM edge_instances e
            JOIN line c ON e.to_node_id = c.next_node
            WHERE e.edge_type_id = @edgeTypeId AND c.depth < @maxDepth
              AND NOT (e.from_node_id = ANY(c.visited))
        )
        SELECT edge_id AS EdgeId, from_node_id AS FromNode, to_node_id AS ToNode,
               next_node AS NextNode,
               weight AS Weight, created_at AS CreatedAt, updated_at AS UpdatedAt, depth AS Depth
        FROM line ORDER BY depth
        """;

    /// <summary>向下游走：从当前节点沿 FromNode→ToNode 正向走（visited 去重防环）。</summary>
    private const string DownstreamCteSql = """
        WITH RECURSIVE line AS (
            SELECT edge_id, from_node_id, to_node_id, weight, created_at, updated_at,
                   to_node_id AS next_node, 0 AS depth, ARRAY[to_node_id::text] AS visited
            FROM edge_instances
            WHERE edge_type_id = @edgeTypeId AND from_node_id = @startNodeId

            UNION ALL

            SELECT e.edge_id, e.from_node_id, e.to_node_id, e.weight, e.created_at, e.updated_at,
                   e.to_node_id, c.depth + 1, c.visited || e.to_node_id::text
            FROM edge_instances e
            JOIN line c ON e.from_node_id = c.next_node
            WHERE e.edge_type_id = @edgeTypeId AND c.depth < @maxDepth
              AND NOT (e.to_node_id = ANY(c.visited))
        )
        SELECT edge_id AS EdgeId, from_node_id AS FromNode, to_node_id AS ToNode,
               next_node AS NextNode,
               weight AS Weight, created_at AS CreatedAt, updated_at AS UpdatedAt, depth AS Depth
        FROM line ORDER BY depth
        """;

    /// <summary>
    /// 无向线路双向展开：忽略 up/down，从当前节点出发沿边两端任意方向游走，
    /// 覆盖起点所在的整个连通线路。visited 记录已到达节点，visited_edges 记录
    /// 已用过的边，二者共同防止往返重复与成环回绕。
    /// </summary>
    private const string UndirectedCteSql = """
        WITH RECURSIVE line AS (
            SELECT edge_id, from_node_id, to_node_id, weight, created_at, updated_at,
                   CASE WHEN from_node_id = @startNodeId THEN to_node_id ELSE from_node_id END AS next_node,
                   0 AS depth,
                   ARRAY[from_node_id::text, to_node_id::text] AS visited,
                   ARRAY[edge_id::text] AS visited_edges
            FROM edge_instances
            WHERE edge_type_id = @edgeTypeId
              AND (from_node_id = @startNodeId OR to_node_id = @startNodeId)

            UNION ALL

            SELECT e.edge_id, e.from_node_id, e.to_node_id, e.weight, e.created_at, e.updated_at,
                   CASE WHEN e.from_node_id = c.next_node THEN e.to_node_id ELSE e.from_node_id END,
                   c.depth + 1,
                   c.visited || (CASE WHEN e.from_node_id = c.next_node
                                      THEN e.to_node_id::text ELSE e.from_node_id::text END),
                   c.visited_edges || e.edge_id::text
            FROM edge_instances e
            JOIN line c ON (e.from_node_id = c.next_node OR e.to_node_id = c.next_node)
            WHERE e.edge_type_id = @edgeTypeId AND c.depth < @maxDepth
              AND NOT (e.edge_id = ANY(c.visited_edges))
              AND NOT (CASE WHEN e.from_node_id = c.next_node
                            THEN e.to_node_id ELSE e.from_node_id END = ANY(c.visited))
        )
        SELECT edge_id AS EdgeId, from_node_id AS FromNode, to_node_id AS ToNode,
               next_node AS NextNode,
               weight AS Weight, created_at AS CreatedAt, updated_at AS UpdatedAt, depth AS Depth
        FROM line ORDER BY depth
        """;

    private sealed record LineEdgeRow(
        string EdgeId, string FromNode, string ToNode, string NextNode,
        decimal? Weight, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt,
        int Depth);

    // ============================================================
    // ③ 拓扑校验（入度/出度/成环）—— 内核复用 EdgeSky 的迭代 Kahn
    // ============================================================

    /// <summary>
    /// 校验线路结构拓扑：入度/出度/成环。
    /// 严格线性线路（IsLine &amp;&amp; IsLinear）下，入度 &gt; 1 与成环计入 Errors；
    /// 非线性线路（或非线路类型）下同样的问题仅作为 Warnings 提示，不影响 Valid。
    /// </summary>
    public async Task<LineValidationResult> ValidateTopologyAsync(
        string edgeTypeId, CancellationToken ct)
    {
        var def = await GetDefinitionAsync(edgeTypeId, ct);

        var pairs = await _db.EdgeInstances
            .Where(e => e.EdgeTypeId == edgeTypeId)
            .Select(e => new { e.FromNodeId, e.ToNodeId })
            .ToListAsync(ct);

        var analysis = EdgeInstanceService.AnalyzeTopology(
            pairs.Select(e => (e.FromNodeId, e.ToNodeId)).ToList());
        var treatAsStrict = def is { IsLine: true, IsLinear: true };

        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var (nodeId, deg) in analysis.Indeg.Where(kv => kv.Value > 1))
        {
            var msg = $"节点 {nodeId} 入度 = {deg}（线路结构入度应 ≤ 1）";
            (treatAsStrict ? errors : warnings).Add(msg);
        }

        foreach (var nodeId in analysis.Cyclic)
        {
            var msg = $"节点 {nodeId} 参与成环（线路结构应无环）";
            (treatAsStrict ? errors : warnings).Add(msg);
        }

        return new LineValidationResult(
            Valid: errors.Count == 0,
            Errors: errors,
            InDegreeMap: analysis.Indeg,
            OutDegreeMap: analysis.Outdeg,
            CyclicNodeIds: analysis.Cyclic,
            EdgeCount: pairs.Count,
            NodeCount: analysis.NodeCount,
            Warnings: warnings);
    }

    // ============================================================
    // ④ 连通分量（多条互不相连的线路）—— O(V+E)
    // ============================================================

    public async Task<List<LineConnectedComponent>> GetConnectedComponentsAsync(
        string edgeTypeId, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);

        // 投影仅取组装 DTO 所需列（匿名类型不进变更跟踪），避免整行实体加载
        var edges = await _db.EdgeInstances
            .Where(e => e.EdgeTypeId == edgeTypeId)
            .Select(e => new { e.EdgeId, e.FromNodeId, e.ToNodeId, e.Weight, e.DisplayName, e.CreatedAt, e.UpdatedAt })
            .ToListAsync(ct);

        // 无向邻接 + 节点→关联边 索引，均 O(V+E) 构建
        var neighbors = new Dictionary<string, HashSet<string>>();
        var incident = new Dictionary<string, List<string>>();

        void Add(string a, string b, string edgeId)
        {
            if (!neighbors.TryGetValue(a, out var sa)) neighbors[a] = sa = new();
            sa.Add(b);
            if (!neighbors.TryGetValue(b, out var sb)) neighbors[b] = sb = new();
            sb.Add(a);
            if (!incident.TryGetValue(a, out var ia)) incident[a] = ia = new();
            ia.Add(edgeId);
            if (!incident.TryGetValue(b, out var ib)) incident[b] = ib = new();
            ib.Add(edgeId);
        }

        var edgeById = edges.ToDictionary(e => e.EdgeId);
        foreach (var e in edges)
            Add(e.FromNodeId, e.ToNodeId, e.EdgeId);

        // 两端 Inode 显示名：整批一次解析
        var names = await _inodeGuards.ResolveNodeNamesAsync(
            edges.SelectMany(e => new[] { e.FromNodeId, e.ToNodeId }), ct);

        var visited = new HashSet<string>();
        var components = new List<LineConnectedComponent>();

        foreach (var start in neighbors.Keys)
        {
            if (visited.Contains(start)) continue;

            var compNodes = new List<string>();
            var compEdgeIds = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                compNodes.Add(cur);

                // 收集该节点关联的全部边（第二遍并入，保证两端已访问的边不漏）
                foreach (var eid in incident.GetValueOrDefault(cur, new()))
                    compEdgeIds.Add(eid);

                foreach (var nbr in neighbors[cur])
                    if (visited.Add(nbr)) queue.Enqueue(nbr);
            }

            var compEdges = compEdgeIds
                .Select(id =>
                {
                    var e = edgeById[id];
                    return new EdgeInstanceDto(
                        EdgeId: id,
                        EdgeTypeId: edgeTypeId,
                        EdgeTypeName: def?.TypeName ?? "",
                        EdgeTypeDisplayName: def?.DisplayName ?? "",
                        FromNodeId: e.FromNodeId, FromNodeText: names.GetValueOrDefault(e.FromNodeId, ""),
                        ToNodeId: e.ToNodeId, ToNodeText: names.GetValueOrDefault(e.ToNodeId, ""),
                        Weight: e.Weight,
                        DisplayName: e.DisplayName,
                        CreatedAt: e.CreatedAt, UpdatedAt: e.UpdatedAt,
                        Properties: null);
                })
                .ToList();

            components.Add(new LineConnectedComponent(compNodes, compEdges, compNodes.Count));
        }

        return components;
    }
}

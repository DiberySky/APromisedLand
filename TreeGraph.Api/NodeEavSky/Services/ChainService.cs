using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>
/// 链式结构（Chain）专用服务。
///
/// 链式结构 = EdgeDefinition.IsChain = true 的一组边实例，
/// 语义上表达"点线相连的链式结构"（如输电线路、物流线路、工序链）。
///
/// 核心查询使用 PostgreSQL WITH RECURSIVE CTE（单次 SQL 拿完整路径）。
/// </summary>
public class ChainService
{
    /// <summary>路径递归的最大展开层数；触顶即视为截断（返回 Truncated=true）。</summary>
    public const int MaxPathDepth = 500;

    private readonly TreeGraphDbContext _db;
    private readonly EdgeInstanceService _edgeInst;
    private readonly IEdgeNodeValidator _nodeValidator;

    public ChainService(
        TreeGraphDbContext db, EdgeInstanceService edgeInst, IEdgeNodeValidator nodeValidator)
    {
        _db = db;
        _edgeInst = edgeInst;
        _nodeValidator = nodeValidator;
    }

    /// <summary>读取边类型定义（跟踪查询，便于变更链路复用同一实例）。</summary>
    public Task<EdgeDefinition?> GetDefinitionAsync(string edgeTypeId, CancellationToken ct)
        => _db.EdgeDefinitions.FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);

    // ============================================================
    // ① 批量创建链（A→B→C→D）
    // ============================================================

    /// <summary>
    /// 全事务创建：按 OrderedNodeIds 顺序创建 from→to 边。
    /// 任何一段失败 → 整体回滚。
    /// 建链前先做整链体检：节点不重复、节点存在/类型匹配、候选图不成环。
    /// </summary>
    public async Task<List<EdgeInstanceDto>> CreateChainAsync(
        string edgeTypeId, CreateChainRequest req, CancellationToken ct)
    {
        if (req.OrderedNodeIds.Count < 2)
            throw new ArgumentException("OrderedNodeIds 至少需要 2 个节点才能形成链");

        if (req.OrderedNodeIds.Count != req.OrderedNodeIds.Distinct().Count())
            throw new ArgumentException(
                "OrderedNodeIds 存在重复节点，无法形成链（链式结构应无环且不可自环）");

        if (req.Weights is not null && req.Weights.Count != req.OrderedNodeIds.Count - 1)
            throw new ArgumentException("Weights 长度应 = OrderedNodeIds.Count - 1");

        var def = await GetDefinitionAsync(edgeTypeId, ct);
        if (def is null)
            throw new InvalidOperationException($"EdgeType 不存在: {edgeTypeId}");

        // 节点存在性 + 声明类型校验（每个节点需同时满足 from/to 节点类型声明）
        foreach (var nodeId in req.OrderedNodeIds)
        {
            await _nodeValidator.EnsureNodeAsync(def.FromNodeType, nodeId, ct);
            if (!string.Equals(def.FromNodeType, def.ToNodeType, StringComparison.Ordinal))
                await _nodeValidator.EnsureNodeAsync(def.ToNodeType, nodeId, ct);
        }

        // 候选图成环检测：现有边 + 待新增链边。
        // 注：IsChain=true 的类型在启用时已通过 ValidateTopology 拒绝既有环，
        // 故此处出现的环必为本次新增引入。
        if (def.IsChain)
        {
            var existing = await _db.EdgeInstances
                .Where(e => e.EdgeTypeId == edgeTypeId)
                .Select(e => new { e.FromNodeId, e.ToNodeId })
                .ToListAsync(ct);

            var pairs = new List<(string From, string To)>(existing.Count + req.OrderedNodeIds.Count - 1);
            pairs.AddRange(existing.Select(e => (e.FromNodeId, e.ToNodeId)));
            for (int i = 0; i < req.OrderedNodeIds.Count - 1; i++)
                pairs.Add((req.OrderedNodeIds[i], req.OrderedNodeIds[i + 1]));

            if (AnalyzeTopology(pairs).Cyclic.Count > 0)
                throw new InvalidOperationException(
                    "新增链会形成环，链式结构应无环（请检查 OrderedNodeIds 是否与既有边构成回路）");
        }

        // NpgsqlRetryingExecutionStrategy 不支持用户自发起事务，须经执行策略包裹后整体执行
        // （与 EavWriteService / E2eBaselineSeeder 的既有约定一致）。
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var result = new List<EdgeInstanceDto>(req.OrderedNodeIds.Count - 1);

                for (int i = 0; i < req.OrderedNodeIds.Count - 1; i++)
                {
                    var fromId = req.OrderedNodeIds[i];
                    var toId = req.OrderedNodeIds[i + 1];
                    var weight = req.Weights?[i];

                    var edge = await _edgeInst.CreateAsync(
                        edgeTypeId,
                        new CreateEdgeRequest
                        {
                            FromNodeId = fromId,
                            ToNodeId = toId,
                            Weight = weight
                        },
                        ct);

                    result.Add(edge!);
                }

                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    // ============================================================
    // ② 完整路径查询（WITH RECURSIVE CTE）
    // ============================================================

    /// <summary>
    /// 从 startNodeId 沿指定方向走到底，返回完整链。
    /// direction=down → 沿 FromNodeId→ToNodeId 往下游走
    /// direction=up   → 沿 ToNodeId→FromNodeId 往上游走
    /// 无向链（IsDirected=false）忽略 direction，统一按双向展开（up/down 结果一致）。
    /// 单次 SQL（WITH RECURSIVE），线性 O(N)；带 visited 去重防环，深度上限 MaxPathDepth。
    /// </summary>
    public async Task<ChainPathResult> GetPathAsync(
        string edgeTypeId, string startNodeId, string direction,
        CancellationToken ct, int maxDepth = MaxPathDepth, EdgeDefinition? def = null)
    {
        // 调用方（如 ChainController 的守卫）已加载定义时直接复用，避免重复查询。
        def ??= await _db.EdgeDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);

        // 无向链（IsDirected=false）：up/down 语义不适用，统一按双向展开。
        var sql = def is { IsDirected: false }
            ? UndirectedCteSql
            : direction == "up" ? UpstreamCteSql : DownstreamCteSql;

        var edges = await _db.Database
            .SqlQueryRaw<ChainEdgeRow>(sql,
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
                FromNodeText: "",
                ToNodeId: row.ToNode,
                ToNodeText: "",
                Weight: row.Weight,
                CreatedAt: row.CreatedAt,
                UpdatedAt: row.UpdatedAt,
                Properties: null));
        }

        return new ChainPathResult(nodeIds, dtos, dtos.Count, truncated);
    }

    /// <summary>向上游走：从当前节点沿 ToNode→FromNode 反向走（visited 去重防环）。</summary>
    private const string UpstreamCteSql = """
        WITH RECURSIVE chain AS (
            SELECT edge_id, from_node_id, to_node_id, weight, created_at, updated_at,
                   from_node_id AS next_node, 0 AS depth, ARRAY[from_node_id::text] AS visited
            FROM edge_instances
            WHERE edge_type_id = @edgeTypeId AND to_node_id = @startNodeId

            UNION ALL

            SELECT e.edge_id, e.from_node_id, e.to_node_id, e.weight, e.created_at, e.updated_at,
                   e.from_node_id, c.depth + 1, c.visited || e.from_node_id::text
            FROM edge_instances e
            JOIN chain c ON e.to_node_id = c.next_node
            WHERE e.edge_type_id = @edgeTypeId AND c.depth < @maxDepth
              AND NOT (e.from_node_id = ANY(c.visited))
        )
        SELECT edge_id AS EdgeId, from_node_id AS FromNode, to_node_id AS ToNode,
               next_node AS NextNode,
               weight AS Weight, created_at AS CreatedAt, updated_at AS UpdatedAt, depth AS Depth
        FROM chain ORDER BY depth
        """;

    /// <summary>向下游走：从当前节点沿 FromNode→ToNode 正向走（visited 去重防环）。</summary>
    private const string DownstreamCteSql = """
        WITH RECURSIVE chain AS (
            SELECT edge_id, from_node_id, to_node_id, weight, created_at, updated_at,
                   to_node_id AS next_node, 0 AS depth, ARRAY[to_node_id::text] AS visited
            FROM edge_instances
            WHERE edge_type_id = @edgeTypeId AND from_node_id = @startNodeId

            UNION ALL

            SELECT e.edge_id, e.from_node_id, e.to_node_id, e.weight, e.created_at, e.updated_at,
                   e.to_node_id, c.depth + 1, c.visited || e.to_node_id::text
            FROM edge_instances e
            JOIN chain c ON e.from_node_id = c.next_node
            WHERE e.edge_type_id = @edgeTypeId AND c.depth < @maxDepth
              AND NOT (e.to_node_id = ANY(c.visited))
        )
        SELECT edge_id AS EdgeId, from_node_id AS FromNode, to_node_id AS ToNode,
               next_node AS NextNode,
               weight AS Weight, created_at AS CreatedAt, updated_at AS UpdatedAt, depth AS Depth
        FROM chain ORDER BY depth
        """;

    /// <summary>
    /// 无向链双向展开：忽略 up/down，从当前节点出发沿边两端任意方向游走，
    /// 覆盖起点所在的整个连通链。visited 记录已到达节点，visited_edges 记录
    /// 已用过的边，二者共同防止往返重复与成环回绕。
    /// </summary>
    private const string UndirectedCteSql = """
        WITH RECURSIVE chain AS (
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
            JOIN chain c ON (e.from_node_id = c.next_node OR e.to_node_id = c.next_node)
            WHERE e.edge_type_id = @edgeTypeId AND c.depth < @maxDepth
              AND NOT (e.edge_id = ANY(c.visited_edges))
              AND NOT (CASE WHEN e.from_node_id = c.next_node
                            THEN e.to_node_id ELSE e.from_node_id END = ANY(c.visited))
        )
        SELECT edge_id AS EdgeId, from_node_id AS FromNode, to_node_id AS ToNode,
               next_node AS NextNode,
               weight AS Weight, created_at AS CreatedAt, updated_at AS UpdatedAt, depth AS Depth
        FROM chain ORDER BY depth
        """;

    private sealed record ChainEdgeRow(
        string EdgeId, string FromNode, string ToNode, string NextNode,
        decimal? Weight, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt,
        int Depth);

    // ============================================================
    // ③ 拓扑校验（入度/出度/成环）—— 迭代 Kahn，无递归
    // ============================================================

    /// <summary>
    /// 校验链式结构拓扑：入度/出度/成环。
    /// 严格线性链（IsChain &amp;&amp; IsLinear）下，入度 &gt; 1 与成环计入 Errors；
    /// 非线性链（或非链式类型）下同样的问题仅作为 Warnings 提示，不影响 Valid。
    /// </summary>
    public async Task<ChainValidationResult> ValidateTopologyAsync(
        string edgeTypeId, CancellationToken ct)
    {
        var def = await GetDefinitionAsync(edgeTypeId, ct);

        var pairs = await _db.EdgeInstances
            .Where(e => e.EdgeTypeId == edgeTypeId)
            .Select(e => new { e.FromNodeId, e.ToNodeId })
            .ToListAsync(ct);

        var analysis = AnalyzeTopology(pairs.Select(e => (e.FromNodeId, e.ToNodeId)).ToList());
        var treatAsStrict = def is { IsChain: true, IsLinear: true };

        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var (nodeId, deg) in analysis.Indeg.Where(kv => kv.Value > 1))
        {
            var msg = $"节点 {nodeId} 入度 = {deg}（链式结构入度应 ≤ 1）";
            (treatAsStrict ? errors : warnings).Add(msg);
        }

        foreach (var nodeId in analysis.Cyclic)
        {
            var msg = $"节点 {nodeId} 参与成环（链式结构应无环）";
            (treatAsStrict ? errors : warnings).Add(msg);
        }

        return new ChainValidationResult(
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
    // ④ 连通分量（多条互不相连的链）—— O(V+E)
    // ============================================================

    public async Task<List<ChainConnectedComponent>> GetConnectedComponentsAsync(
        string edgeTypeId, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);

        // 投影仅取组装 DTO 所需列（匿名类型不进变更跟踪），避免整行实体加载
        var edges = await _db.EdgeInstances
            .Where(e => e.EdgeTypeId == edgeTypeId)
            .Select(e => new { e.EdgeId, e.FromNodeId, e.ToNodeId, e.Weight, e.CreatedAt, e.UpdatedAt })
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

        var visited = new HashSet<string>();
        var components = new List<ChainConnectedComponent>();

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
                        FromNodeId: e.FromNodeId, FromNodeText: "",
                        ToNodeId: e.ToNodeId, ToNodeText: "",
                        Weight: e.Weight,
                        CreatedAt: e.CreatedAt, UpdatedAt: e.UpdatedAt,
                        Properties: null);
                })
                .ToList();

            components.Add(new ChainConnectedComponent(compNodes, compEdges, compNodes.Count));
        }

        return components;
    }

    // ============================================================
    // 拓扑分析内核（迭代 Kahn）
    // ============================================================

    private sealed record TopologyAnalysis(
        Dictionary<string, int> Indeg,
        Dictionary<string, int> Outdeg,
        List<string> Cyclic,
        int NodeCount);

    /// <summary>
    /// 迭代 Kahn 拓扑排序：返回入度表、出度表、未能消解的节点（= 成环节点）。
    /// 无递归，避免长链导致的栈溢出；O(V+E)。
    /// 注：环下游的节点也可能留在 Cyclic 中（Kahn 的已知特性），仅用于诊断提示。
    /// </summary>
    private static TopologyAnalysis AnalyzeTopology(List<(string From, string To)> edges)
    {
        var indeg = new Dictionary<string, int>();
        var outdeg = new Dictionary<string, int>();
        var adj = new Dictionary<string, List<string>>();

        foreach (var (from, to) in edges)
        {
            if (!indeg.ContainsKey(from)) indeg[from] = 0;
            indeg[to] = indeg.GetValueOrDefault(to) + 1;
            outdeg[from] = outdeg.GetValueOrDefault(from) + 1;
            if (!outdeg.ContainsKey(to)) outdeg[to] = 0;

            if (!adj.TryGetValue(from, out var list)) adj[from] = list = new();
            list.Add(to);
        }

        var remaining = new Dictionary<string, int>(indeg);
        var queue = new Queue<string>(remaining.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var processed = new HashSet<string>();

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            processed.Add(node);

            foreach (var next in adj.GetValueOrDefault(node, new()))
            {
                if (!remaining.ContainsKey(next)) continue;
                remaining[next]--;
                if (remaining[next] == 0) queue.Enqueue(next);
            }
        }

        var cyclic = indeg.Keys.Where(n => !processed.Contains(n)).ToList();
        return new TopologyAnalysis(indeg, outdeg, cyclic, indeg.Count);
    }
}

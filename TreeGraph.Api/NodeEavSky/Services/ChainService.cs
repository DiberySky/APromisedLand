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
    private readonly TreeGraphDbContext _db;
    private readonly EdgeInstanceService _edgeInst;

    public ChainService(TreeGraphDbContext db, EdgeInstanceService edgeInst)
    {
        _db = db;
        _edgeInst = edgeInst;
    }

    // ============================================================
    // ① 批量创建链（A→B→C→D）
    // ============================================================

    /// <summary>
    /// 全事务创建：按 OrderedNodeIds 顺序创建 from→to 边。
    /// 任何一段失败 → 整体回滚（Savepoint 策略）。
    /// 链式入度约束在 EdgeInstanceService.CreateAsync 内校验。
    /// </summary>
    public async Task<List<EdgeInstanceDto>> CreateChainAsync(
        string edgeTypeId, CreateChainRequest req, CancellationToken ct)
    {
        if (req.OrderedNodeIds.Count < 2)
            throw new ArgumentException("OrderedNodeIds 至少需要 2 个节点才能形成链");

        if (req.Weights is not null && req.Weights.Count != req.OrderedNodeIds.Count - 1)
            throw new ArgumentException("Weights 长度应 = OrderedNodeIds.Count - 1");

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
    }

    // ============================================================
    // ② 完整路径查询（WITH RECURSIVE CTE）
    // ============================================================

    /// <summary>
    /// 从 startNodeId 沿指定方向走到底，返回完整链。
    /// direction=down → 沿 FromNodeId→ToNodeId 往下游走
    /// direction=up   → 沿 ToNodeId→FromNodeId 往上游走
    /// 单次 SQL（WITH RECURSIVE），线性 O(N)。
    /// </summary>
    public async Task<ChainPathResult> GetPathAsync(
        string edgeTypeId, string startNodeId, string direction, CancellationToken ct)
    {
        // SQL 模板：direction 决定递归方向
        // down:  edge_type_id = @et AND from_node_id = current
        // up:    edge_type_id = @et AND to_node_id   = current
        var sql = direction == "up" ? UpstreamCteSql : DownstreamCteSql;

        var edges = await _db.Database
            .SqlQueryRaw<ChainEdgeRow>(sql,
                new Npgsql.NpgsqlParameter("edgeTypeId", edgeTypeId),
                new Npgsql.NpgsqlParameter("startNodeId", startNodeId))
            .ToListAsync(ct);

        // 组装结果
        var orderedEdges = edges.OrderBy(e => e.Depth).ToList();
        var nodeIds = new List<string> { startNodeId };
        var dtos = new List<EdgeInstanceDto>();

        foreach (var row in orderedEdges)
        {
            nodeIds.Add(row.ToNode);
            // 构建 DTO（类型名/显示名会由 EdgeInstanceService.BuildDtoAsync 自动填充）
            // 这里只拿 path 查询，DTO 的属性值走后续 GetById
            dtos.Add(new EdgeInstanceDto(
                EdgeId: row.EdgeId,
                EdgeTypeId: edgeTypeId,
                EdgeTypeName: "",
                EdgeTypeDisplayName: "",
                FromNodeId: row.FromNode,
                FromNodeText: "",
                ToNodeId: row.ToNode,
                ToNodeText: "",
                Weight: row.Weight,
                CreatedAt: row.CreatedAt,
                UpdatedAt: row.UpdatedAt,
                Properties: null));
        }

        // 批量填充 EdgeTypeName/DisplayName（单次查询）
        if (dtos.Count > 0)
        {
            var typeName = await _db.EdgeDefinitions
                .Where(d => d.EdgeTypeId == edgeTypeId)
                .Select(d => new { d.TypeName, d.DisplayName })
                .FirstOrDefaultAsync(ct);

            if (typeName is not null)
            {
                for (int i = 0; i < dtos.Count; i++)
                    dtos[i] = dtos[i] with
                    {
                        EdgeTypeName = typeName.TypeName,
                        EdgeTypeDisplayName = typeName.DisplayName
                    };
            }
        }

        return new ChainPathResult(nodeIds, dtos, dtos.Count);
    }

    /// <summary>向上游走：ToNode = current → 找 FromNode。</summary>
    private const string UpstreamCteSql = @"
        WITH RECURSIVE chain AS (
            SELECT edge_id, from_node_id, to_node_id, weight, created_at, updated_at, 0 AS depth
            FROM edge_instances
            WHERE edge_type_id = @edgeTypeId AND to_node_id = @startNodeId

            UNION ALL

            SELECT e.edge_id, e.from_node_id, e.to_node_id, e.weight, e.created_at, e.updated_at, c.depth + 1
            FROM edge_instances e
            JOIN chain c ON e.to_node_id = c.from_node_id
            WHERE e.edge_type_id = @edgeTypeId AND c.depth < 1000
        )
        SELECT edge_id AS EdgeId, from_node_id AS FromNode, to_node_id AS ToNode,
               weight AS Weight, created_at AS CreatedAt, updated_at AS UpdatedAt, depth AS Depth
        FROM chain ORDER BY depth";

    /// <summary>向下游走：FromNode = current → 找 ToNode。</summary>
    private const string DownstreamCteSql = @"
        WITH RECURSIVE chain AS (
            SELECT edge_id, from_node_id, to_node_id, weight, created_at, updated_at, 0 AS depth
            FROM edge_instances
            WHERE edge_type_id = @edgeTypeId AND from_node_id = @startNodeId

            UNION ALL

            SELECT e.edge_id, e.from_node_id, e.to_node_id, e.weight, e.created_at, e.updated_at, c.depth + 1
            FROM edge_instances e
            JOIN chain c ON e.from_node_id = c.to_node_id
            WHERE e.edge_type_id = @edgeTypeId AND c.depth < 1000
        )
        SELECT edge_id AS EdgeId, from_node_id AS FromNode, to_node_id AS ToNode,
               weight AS Weight, created_at AS CreatedAt, updated_at AS UpdatedAt, depth AS Depth
        FROM chain ORDER BY depth";

    private sealed record ChainEdgeRow(
        string EdgeId, string FromNode, string ToNode,
        decimal? Weight, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt,
        int Depth);

    // ============================================================
    // ③ 拓扑校验（入度/出度/成环）
    // ============================================================

    public async Task<ChainValidationResult> ValidateTopologyAsync(
        string edgeTypeId, CancellationToken ct)
    {
        var edges = await _db.EdgeInstances
            .Where(e => e.EdgeTypeId == edgeTypeId)
            .ToListAsync(ct);

        var errors = new List<string>();
        var indeg = new Dictionary<string, int>();
        var outdeg = new Dictionary<string, int>();

        foreach (var e in edges)
        {
            indeg.TryAdd(e.ToNodeId, 0);
            indeg[e.ToNodeId]++;

            outdeg.TryAdd(e.FromNodeId, 0);
            outdeg[e.FromNodeId]++;
        }

        // 入度 > 1 错误
        foreach (var (nodeId, deg) in indeg.Where(kv => kv.Value > 1))
            errors.Add($"节点 {nodeId} 入度 = {deg}（链式结构入度应 ≤ 1）");

        // 成环检测（对 IsLinear=true 类型）
        var cyclic = DetectCycles(edges);
        foreach (var nodeId in cyclic)
            errors.Add($"节点 {nodeId} 参与成环（链式结构应无环）");

        // 节点总数
        var nodeSet = new HashSet<string>();
        foreach (var e in edges)
        {
            nodeSet.Add(e.FromNodeId);
            nodeSet.Add(e.ToNodeId);
        }

        var valid = errors.Count == 0;

        return new ChainValidationResult(
            Valid: valid,
            Errors: errors,
            InDegreeMap: indeg,
            OutDegreeMap: outdeg,
            CyclicNodeIds: cyclic,
            EdgeCount: edges.Count,
            NodeCount: nodeSet.Count);
    }

    /// <summary>DFS 成环检测：访问中节点在递归栈上再次被访问到 = 有环。</summary>
    private static List<string> DetectCycles(List<EdgeInstance> edges)
    {
        var adj = edges
            .GroupBy(e => e.FromNodeId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ToNodeId).ToList());

        var state = new Dictionary<string, int>(); // 0=未访问, 1=访问中, 2=完成
        var cyclic = new HashSet<string>();

        foreach (var node in adj.Keys)
        {
            if (!state.ContainsKey(node))
                DfsCycle(node, adj, state, cyclic);
        }

        return cyclic.ToList();
    }

    private static void DfsCycle(string node,
        Dictionary<string, List<string>> adj,
        Dictionary<string, int> state,
        HashSet<string> cyclic)
    {
        state[node] = 1; // 访问中
        foreach (var next in adj.GetValueOrDefault(node, new()))
        {
            if (!state.TryGetValue(next, out var s))
            {
                DfsCycle(next, adj, state, cyclic);
            }
            else if (s == 1) // 访问中 → 发现回边
            {
                cyclic.Add(node);
                cyclic.Add(next);
            }
        }
        state[node] = 2; // 完成
    }

    // ============================================================
    // ④ 连通分量（多条互不相连的链）
    // ============================================================

    public async Task<List<ChainConnectedComponent>> GetConnectedComponentsAsync(
        string edgeTypeId, CancellationToken ct)
    {
        var edges = await _db.EdgeInstances
            .Where(e => e.EdgeTypeId == edgeTypeId)
            .ToListAsync(ct);

        // 建图（无向版：链式连通分量看的是节点是否可达）
        var neighbors = new Dictionary<string, HashSet<string>>();
        void AddEdge(string a, string b)
        {
            neighbors.TryAdd(a, new());
            neighbors.TryAdd(b, new());
            neighbors[a].Add(b);
            neighbors[b].Add(a);
        }

        foreach (var e in edges)
            AddEdge(e.FromNodeId, e.ToNodeId);

        // BFS 标记连通分量
        var visited = new HashSet<string>();
        var components = new List<ChainConnectedComponent>();
        var edgeById = edges.ToDictionary(e => e.EdgeId);

        foreach (var node in neighbors.Keys)
        {
            if (visited.Contains(node)) continue;

            var queue = new Queue<string>();
            var compNodes = new List<string>();
            var compEdgeIds = new HashSet<string>();

            queue.Enqueue(node);
            visited.Add(node);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                compNodes.Add(cur);
                foreach (var nbr in neighbors[cur])
                {
                    if (!visited.Add(nbr)) continue;
                    queue.Enqueue(nbr);

                    // 收集这条边
                    var edge = edges.FirstOrDefault(e =>
                        (e.FromNodeId == cur && e.ToNodeId == nbr) ||
                        (e.FromNodeId == nbr && e.ToNodeId == cur));
                    if (edge is not null) compEdgeIds.Add(edge.EdgeId);
                }
            }

            var compEdges = compEdgeIds
                .Select(id => edgeById[id])
                .Select(e => new EdgeInstanceDto(
                    EdgeId: e.EdgeId, EdgeTypeId: e.EdgeTypeId,
                    EdgeTypeName: "", EdgeTypeDisplayName: "",
                    FromNodeId: e.FromNodeId, FromNodeText: "",
                    ToNodeId: e.ToNodeId, ToNodeText: "",
                    Weight: e.Weight,
                    CreatedAt: e.CreatedAt, UpdatedAt: e.UpdatedAt,
                    Properties: null))
                .ToList();

            components.Add(new ChainConnectedComponent(compNodes, compEdges, compNodes.Count));
        }

        return components;
    }
}

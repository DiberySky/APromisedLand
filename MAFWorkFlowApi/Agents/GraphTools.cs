using System.Text;
using System.Text.Json;
using MAFWorkFlowApi.Infrastructure;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 图查询工具集。所有方法都被 GraphAgentService 用 AIFunctionFactory 包装成
/// AITool 交给 LLM，由 LLM 自主决定调用哪个。
/// 依赖现有的 LiteGraphRestClient（复用其 BaseAddress + Auth）。
/// </summary>
public sealed class GraphTools
{
    private const int MaxResults = 1000;

    private readonly LiteGraphRestClient _rest;
    private readonly ILogger<GraphTools> _logger;
    private readonly ToolCallContext _toolCtx;

    public GraphTools(
        LiteGraphRestClient rest,
        ILogger<GraphTools> logger,
        ToolCallContext toolCtx)
    {
        _rest = rest;
        _logger = logger;
        _toolCtx = toolCtx;
    }

    // ══════════════════════════════════════════════════════
    // 工具 1：邻居查询
    // ══════════════════════════════════════════════════════

    public async Task<string> GetNeighborsAsync(
        string nodeName, string graphName, CancellationToken ct = default)
    {
        _toolCtx.Record("GetNeighbors");

        _logger.LogInformation(
            "[Tool] GetNeighbors(node={Node}, graph={Graph})", nodeName, graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null)
            return $"未找到图 '{graphName}'。";

        var target = nodes.FirstOrDefault(n =>
            n.Name.Equals(nodeName, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            return $"在图 '{graphName}' 中未找到节点 '{nodeName}'。";

        var nodeMap = nodes.ToDictionary(n => n.Guid, n => n.Name);
        var lines = new List<string>();

        foreach (var e in edges)
        {
            if (e.From == target.Guid && nodeMap.TryGetValue(e.To, out var toName))
                lines.Add($"- {toName}（出边 {e.Name}）");
            if (e.To == target.Guid && nodeMap.TryGetValue(e.From, out var fromName))
                lines.Add($"- {fromName}（入边 {e.Name}）");
        }

        return lines.Count == 0
            ? $"'{nodeName}' 没有邻居节点。"
            : $"'{nodeName}' 的邻居：\n{string.Join("\n", lines)}";
    }

    // ══════════════════════════════════════════════════════
    // 工具 2：两节点关系
    // ══════════════════════════════════════════════════════

    public async Task<string> GetRelationsAsync(
        string nodeA, string nodeB, string graphName, CancellationToken ct = default)
    {
        _toolCtx.Record("GetRelations");

        _logger.LogInformation(
            "[Tool] GetRelations(a={A}, b={B}, graph={Graph})", nodeA, nodeB, graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null)
            return $"未找到图 '{graphName}'。";

        var nameToNode = nodes
            .GroupBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        if (!nameToNode.TryGetValue(nodeA, out var a))
            return $"未找到节点 '{nodeA}'。";
        if (!nameToNode.TryGetValue(nodeB, out var b))
            return $"未找到节点 '{nodeB}'。";

        var lines = new List<string>();
        foreach (var e in edges)
        {
            if (e.From == a.Guid && e.To == b.Guid)
                lines.Add($"- {nodeA} --[{e.Name}]--> {nodeB}（{nodeA} 是 {nodeB} 的 {e.Name}）");
            if (e.From == b.Guid && e.To == a.Guid)
                lines.Add($"- {nodeB} --[{e.Name}]--> {nodeA}（{nodeB} 是 {nodeA} 的 {e.Name}）");
        }

        return lines.Count == 0
            ? $"'{nodeA}' 和 '{nodeB}' 之间没有直接关系。"
            : $"'{nodeA}' 和 '{nodeB}' 的关系：\n{string.Join("\n", lines)}";
    }

    // ══════════════════════════════════════════════════════
    // 工具 3：列出所有节点
    // ══════════════════════════════════════════════════════

    public async Task<string> ListAllNodesAsync(
        string graphName, CancellationToken ct = default)
    {
        _toolCtx.Record("ListAllNodes");

        _logger.LogInformation("[Tool] ListAllNodes(graph={Graph})", graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null)
            return $"未找到图 '{graphName}'。";

        if (nodes.Count == 0)
            return $"图 '{graphName}' 没有节点。";

        var nodeMap = nodes.ToDictionary(n => n.Guid, n => n.Name);
        var sb = new StringBuilder();
        sb.AppendLine($"图 '{graphName}' 共 {nodes.Count} 个节点、{edges.Count} 条边。");
        sb.AppendLine("节点列表：");
        foreach (var n in nodes)
            sb.AppendLine($"- {n.Name}");

        if (edges.Count > 0)
        {
            sb.AppendLine("关系列表：");
            foreach (var e in edges)
            {
                var f = nodeMap.TryGetValue(e.From, out var fn) ? fn : e.From.ToString();
                var t = nodeMap.TryGetValue(e.To, out var tn) ? tn : e.To.ToString();
                sb.AppendLine($"- {f} --[{e.Name}]--> {t}");
            }
        }

        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════
    // 工具 4：图规模
    // ══════════════════════════════════════════════════════

    public async Task<string> GetGraphSizeAsync(
        string graphName, CancellationToken ct = default)
    {
        _toolCtx.Record("GetGraphSize");

        _logger.LogInformation("[Tool] GetGraphSize(graph={Graph})", graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null)
            return $"未找到图 '{graphName}'。";

        return $"图 '{graphName}' 共 {nodes.Count} 个节点、{edges.Count} 条边。";
    }

    // ══════════════════════════════════════════════════════
    // 工具 5：最短路径
    // ══════════════════════════════════════════════════════

    public async Task<string> FindPathAsync(
        string fromNode, string toNode, string graphName, CancellationToken ct = default)
    {
        _toolCtx.Record("FindPath");

        _logger.LogInformation(
            "[Tool] FindPath(from={From}, to={To}, graph={Graph})", fromNode, toNode, graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null)
            return $"未找到图 '{graphName}'。";

        var nameToNode = nodes
            .GroupBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        if (!nameToNode.TryGetValue(fromNode, out var start))
            return $"未找到起始节点 '{fromNode}'。";
        if (!nameToNode.TryGetValue(toNode, out var end))
            return $"未找到目标节点 '{toNode}'。";

        var adjacency = nodes.ToDictionary(n => n.Guid, _ => new List<Guid>());
        foreach (var e in edges)
        {
            if (adjacency.ContainsKey(e.From)) adjacency[e.From].Add(e.To);
            if (adjacency.ContainsKey(e.To)) adjacency[e.To].Add(e.From);
        }

        var guidToName = nodes.ToDictionary(n => n.Guid, n => n.Name);
        var visited = new HashSet<Guid> { start.Guid };
        var queue = new Queue<List<Guid>>();
        queue.Enqueue(new List<Guid> { start.Guid });

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var last = path[^1];

            if (last == end.Guid)
            {
                var names = path.Select(g => guidToName[g]);
                return $"'{fromNode}' 到 '{toNode}' 的最短路径（{path.Count - 1} 步）：\n" +
                       string.Join(" → ", names);
            }

            foreach (var next in adjacency[last])
            {
                if (visited.Add(next))
                    queue.Enqueue(new List<Guid>(path) { next });
            }
        }

        return $"'{fromNode}' 和 '{toNode}' 之间不存在路径。";
    }

    // ══════════════════════════════════════════════════════
    // 工具 6：★ 新增 — 关键词搜索节点
    // ══════════════════════════════════════════════════════

    public async Task<string> SearchNodesAsync(
        string keyword, string graphName, CancellationToken ct = default)
    {
        _toolCtx.Record("SearchNodes");

        _logger.LogInformation(
            "[Tool] SearchNodes(keyword={Keyword}, graph={Graph})", keyword, graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null)
            return $"未找到图 '{graphName}'。";

        if (string.IsNullOrWhiteSpace(keyword))
            return "关键词不能为空。";

        var trimmed = keyword.Trim();
        var matches = nodes
            .Where(n => n.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
            return $"在图 '{graphName}' 中未找到名称包含 '{trimmed}' 的节点。";

        var nodeMap = nodes.ToDictionary(n => n.Guid, n => n.Name);
        var matchGuids = matches.Select(m => m.Guid).ToHashSet();

        var sb = new StringBuilder();
        sb.AppendLine($"在图 '{graphName}' 中找到 {matches.Count} 个名称包含 '{trimmed}' 的节点：");
        sb.AppendLine("节点：");
        foreach (var m in matches)
            sb.AppendLine($"- {m.Name}");

        // 顺手给出这些节点之间的相互关系（不含跨节点的边，避免信息过载）
        var internalEdges = edges
            .Where(e => matchGuids.Contains(e.From) && matchGuids.Contains(e.To))
            .ToList();

        if (internalEdges.Count > 0)
        {
            sb.AppendLine("它们之间的关系：");
            foreach (var e in internalEdges)
            {
                var f = nodeMap.TryGetValue(e.From, out var fn) ? fn : e.From.ToString();
                var t = nodeMap.TryGetValue(e.To, out var tn) ? tn : e.To.ToString();
                sb.AppendLine($"- {f} --[{e.Name}]--> {t}");
            }
        }

        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════
    // 工具 7：★ 新增 — N 跳子图探索
    // ══════════════════════════════════════════════════════

    public async Task<string> GetSubgraphAsync(
        string startNode, int hops, string graphName, CancellationToken ct = default)
    {
        _toolCtx.Record("GetSubgraph");

        // 限制跳数 1-3，避免 LLM 传超大值
        var safeHops = Math.Clamp(hops, 1, 3);

        _logger.LogInformation(
            "[Tool] GetSubgraph(start={Start}, hops={Hops}, graph={Graph})",
            startNode, safeHops, graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null)
            return $"未找到图 '{graphName}'。";

        var nameToNode = nodes
            .GroupBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        if (!nameToNode.TryGetValue(startNode, out var start))
            return $"未找到起始节点 '{startNode}'。";

        // 构建无向邻接表
        var adjacency = nodes.ToDictionary(n => n.Guid, _ => new List<Guid>());
        foreach (var e in edges)
        {
            if (adjacency.ContainsKey(e.From)) adjacency[e.From].Add(e.To);
            if (adjacency.ContainsKey(e.To)) adjacency[e.To].Add(e.From);
        }

        // BFS 限定跳数
        var visited = new HashSet<Guid> { start.Guid };
        var currentLevel = new List<Guid> { start.Guid };

        for (int h = 0; h < safeHops; h++)
        {
            var nextLevel = new List<Guid>();
            foreach (var guid in currentLevel)
            {
                foreach (var next in adjacency[guid])
                {
                    if (visited.Add(next))
                        nextLevel.Add(next);
                }
            }
            currentLevel = nextLevel;
            if (currentLevel.Count == 0) break;
        }

        var nodeMap = nodes.ToDictionary(n => n.Guid, n => n.Name);
        var subgraphNodes = nodes.Where(n => visited.Contains(n.Guid)).ToList();
        var subgraphEdges = edges
            .Where(e => visited.Contains(e.From) && visited.Contains(e.To))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"从 '{startNode}' 出发 {safeHops} 跳内的子图：");
        sb.AppendLine($"共 {subgraphNodes.Count} 个节点、{subgraphEdges.Count} 条边。");
        sb.AppendLine("节点：");
        foreach (var n in subgraphNodes)
        {
            var marker = n.Guid == start.Guid ? "（起始）" : "";
            sb.AppendLine($"- {n.Name}{marker}");
        }

        if (subgraphEdges.Count > 0)
        {
            sb.AppendLine("关系：");
            foreach (var e in subgraphEdges)
            {
                var f = nodeMap.TryGetValue(e.From, out var fn) ? fn : e.From.ToString();
                var t = nodeMap.TryGetValue(e.To, out var tn) ? tn : e.To.ToString();
                sb.AppendLine($"- {f} --[{e.Name}]--> {t}");
            }
        }

        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════
    // 工具 8：★ 新增 — 节点的出边和入边（区分方向）
    // ══════════════════════════════════════════════════════

    public async Task<string> GetEdgesOfNodeAsync(
        string nodeName, string graphName, CancellationToken ct = default)
    {
        _toolCtx.Record("GetEdgesOfNode");

        _logger.LogInformation(
            "[Tool] GetEdgesOfNode(node={Node}, graph={Graph})", nodeName, graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null)
            return $"未找到图 '{graphName}'。";

        var target = nodes.FirstOrDefault(n =>
            n.Name.Equals(nodeName, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            return $"在图 '{graphName}' 中未找到节点 '{nodeName}'。";

        var nodeMap = nodes.ToDictionary(n => n.Guid, n => n.Name);
        var outEdges = new List<string>();
        var inEdges = new List<string>();

        foreach (var e in edges)
        {
            if (e.From == target.Guid && nodeMap.TryGetValue(e.To, out var toName))
                outEdges.Add($"- {nodeName} --[{e.Name}]--> {toName}");
            if (e.To == target.Guid && nodeMap.TryGetValue(e.From, out var fromName))
                inEdges.Add($"- {fromName} --[{e.Name}]--> {nodeName}");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"节点 '{nodeName}' 的出边和入边：");

        if (outEdges.Count > 0)
        {
            sb.AppendLine($"出边（{outEdges.Count} 条）：");
            foreach (var line in outEdges)
                sb.AppendLine(line);
        }
        else
        {
            sb.AppendLine("出边：无");
        }

        if (inEdges.Count > 0)
        {
            sb.AppendLine($"入边（{inEdges.Count} 条）：");
            foreach (var line in inEdges)
                sb.AppendLine(line);
        }
        else
        {
            sb.AppendLine("入边：无");
        }

        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════
    // 私有辅助：从 LiteGraph REST 加载图数据
    // ══════════════════════════════════════════════════════

    private sealed record GraphMeta(Guid Guid, string Name);
    private sealed record NodeMeta(Guid Guid, string Name);
    private sealed record EdgeMeta(Guid From, Guid To, string Name);

    private async Task<(GraphMeta? Graph, List<NodeMeta> Nodes, List<EdgeMeta> Edges)>
        LoadGraphAsync(string graphName, CancellationToken ct)
    {
        try
        {
            var graphGuid = await FindGraphGuidAsync(graphName, ct);
            if (graphGuid is null) return (null, new(), new());

            var nodes = await LoadNodesAsync(graphGuid.Value, ct);
            var edges = await LoadEdgesAsync(graphGuid.Value, ct);

            return (new GraphMeta(graphGuid.Value, graphName), nodes, edges);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载图 '{Graph}' 数据失败", graphName);
            return (null, new(), new());
        }
    }

    private async Task<Guid?> FindGraphGuidAsync(string graphName, CancellationToken ct)
    {
        var path = $"/v1.0/tenants/{_rest.TenantGuid}/graphs";
        var json = await _rest.GetAsync(path, ct);
        if (json is null) return null;

        var root = json.Value;
        if (!root.TryGetProperty("Objects", out var objs) ||
            objs.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var g in objs.EnumerateArray())
        {
            var name = g.TryGetProperty("Name", out var n) ? n.GetString() : null;
            if (string.Equals(name, graphName, StringComparison.OrdinalIgnoreCase))
                return g.TryGetProperty("GUID", out var gg) ? gg.GetGuid() : null;
        }
        return null;
    }

    private async Task<List<NodeMeta>> LoadNodesAsync(Guid graphGuid, CancellationToken ct)
    {
        var path = $"/v1.0/tenants/{_rest.TenantGuid}/graphs/{graphGuid}/nodes";
        var json = await _rest.GetAsync(path, ct);
        var list = new List<NodeMeta>();
        if (json is null) return list;

        if (!json.Value.TryGetProperty("Objects", out var objs) ||
            objs.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var n in objs.EnumerateArray())
        {
            var guid = n.TryGetProperty("GUID", out var g) ? g.GetGuid() : Guid.Empty;
            var name = n.TryGetProperty("Name", out var nm) ? nm.GetString() ?? "" : "";
            if (guid != Guid.Empty && !string.IsNullOrWhiteSpace(name))
                list.Add(new NodeMeta(guid, name));
        }
        return list;
    }

    private async Task<List<EdgeMeta>> LoadEdgesAsync(Guid graphGuid, CancellationToken ct)
    {
        var path = $"/v1.0/tenants/{_rest.TenantGuid}/graphs/{graphGuid}/edges";
        var json = await _rest.GetAsync(path, ct);
        var list = new List<EdgeMeta>();
        if (json is null) return list;

        if (!json.Value.TryGetProperty("Objects", out var objs) ||
            objs.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var e in objs.EnumerateArray())
        {
            var from = e.TryGetProperty("From", out var f) ? f.GetGuid() : Guid.Empty;
            var to = e.TryGetProperty("To", out var t) ? t.GetGuid() : Guid.Empty;
            var name = e.TryGetProperty("Name", out var nm) ? nm.GetString() ?? "" : "";
            if (from != Guid.Empty && to != Guid.Empty)
                list.Add(new EdgeMeta(from, to, name));
        }
        return list;
    }
}
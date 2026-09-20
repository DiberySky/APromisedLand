using System.Text;
using System.Text.Json;
using MAFWorkFlowApi.Infrastructure;
using Microsoft.Extensions.Caching.Memory;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 图查询工具集。所有方法都被 GraphAgentService 用 AIFunctionFactory 包装成
/// AITool 交给 LLM，由 LLM 自主决定调用哪个。
/// 每个方法都通过 TrackAsync 记录调用详情（参数、结果、耗时）。
/// 节点名解析采用宽松策略（ResolveNode），容忍 LLM 提取参数时的小抖动。
/// </summary>
public sealed class GraphTools
{
    private const int MaxResults = 1000;

    private readonly LiteGraphRestClient _rest;
    private readonly ILogger<GraphTools> _logger;
    private readonly ToolCallContext _toolCtx;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public GraphTools(
        LiteGraphRestClient rest,
        ILogger<GraphTools> logger,
        ToolCallContext toolCtx,
        IMemoryCache cache)
    {
        _rest = rest;
        _logger = logger;
        _toolCtx = toolCtx;
        _cache = cache;
    }

    // ══════════════════════════════════════════════════════
    // Track 包装器：统一记录调用详情 + 缓存
    // ══════════════════════════════════════════════════════

    private async Task<string> TrackAsync(
        string toolName, string arguments, Func<Task<string>> body)
    {
        var cacheKey = $"{toolName}|{arguments}";

        // 1. 尝试缓存命中
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
        {
            var hitRecord = _toolCtx.BeginCall(toolName, arguments);
            hitRecord.Complete(cached, fromCache: true);
            _logger.LogDebug("[Cache HIT] {Key}", cacheKey);
            return cached;
        }

        // 2. 缓存未命中：执行
        var record = _toolCtx.BeginCall(toolName, arguments);
        try
        {
            var result = await body();
            record.Complete(result, fromCache: false);

            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl,
                Size = 1
            });

            _logger.LogDebug("[Cache MISS→SET] {Key}", cacheKey);
            return result;
        }
        catch (Exception ex)
        {
            record.Fail(ex.Message);
            throw;
        }
    }

    // ══════════════════════════════════════════════════════
    // 工具 1：邻居查询
    // ══════════════════════════════════════════════════════

    public Task<string> GetNeighborsAsync(
        string nodeName, string graphName, CancellationToken ct = default)
        => TrackAsync("GetNeighbors", $"nodeName={nodeName}, graphName={graphName}",
            () => GetNeighborsCoreAsync(nodeName, graphName, ct));

    private async Task<string> GetNeighborsCoreAsync(
        string nodeName, string graphName, CancellationToken ct)
    {
        _logger.LogInformation(
            "[Tool] GetNeighbors(node={Node}, graph={Graph})", nodeName, graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null) return $"未找到图 '{graphName}'。";

        // ★ 宽松解析
        var target = ResolveNode(nodes, nodeName, _logger);
        if (target is null) return $"在图 '{graphName}' 中未找到节点 '{nodeName}'。";

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
            ? $"'{target.Name}' 没有邻居节点。"
            : $"'{target.Name}' 的邻居：\n{string.Join("\n", lines)}";
    }

    // ══════════════════════════════════════════════════════
    // 工具 2：两节点关系
    // ══════════════════════════════════════════════════════

    public Task<string> GetRelationsAsync(
        string nodeA, string nodeB, string graphName, CancellationToken ct = default)
        => TrackAsync("GetRelations", $"nodeA={nodeA}, nodeB={nodeB}, graphName={graphName}",
            () => GetRelationsCoreAsync(nodeA, nodeB, graphName, ct));

    private async Task<string> GetRelationsCoreAsync(
        string nodeA, string nodeB, string graphName, CancellationToken ct)
    {
        _logger.LogInformation(
            "[Tool] GetRelations(a={A}, b={B}, graph={Graph})", nodeA, nodeB, graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null) return $"未找到图 '{graphName}'。";

        // ★ 宽松解析
        var a = ResolveNode(nodes, nodeA, _logger);
        if (a is null) return $"未找到节点 '{nodeA}'。";
        var b = ResolveNode(nodes, nodeB, _logger);
        if (b is null) return $"未找到节点 '{nodeB}'。";

        var lines = new List<string>();
        foreach (var e in edges)
        {
            if (e.From == a.Guid && e.To == b.Guid)
                lines.Add($"- {a.Name} --[{e.Name}]--> {b.Name}（{a.Name} 是 {b.Name} 的 {e.Name}）");
            if (e.From == b.Guid && e.To == a.Guid)
                lines.Add($"- {b.Name} --[{e.Name}]--> {a.Name}（{b.Name} 是 {a.Name} 的 {e.Name}）");
        }

        return lines.Count == 0
            ? $"'{a.Name}' 和 '{b.Name}' 之间没有直接关系。"
            : $"'{a.Name}' 和 '{b.Name}' 的关系：\n{string.Join("\n", lines)}";
    }

    // ══════════════════════════════════════════════════════
    // 工具 3：列出所有节点
    // ══════════════════════════════════════════════════════

    public Task<string> ListAllNodesAsync(
        string graphName, CancellationToken ct = default)
        => TrackAsync("ListAllNodes", $"graphName={graphName}",
            () => ListAllNodesCoreAsync(graphName, ct));

    private async Task<string> ListAllNodesCoreAsync(string graphName, CancellationToken ct)
    {
        _logger.LogInformation("[Tool] ListAllNodes(graph={Graph})", graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null) return $"未找到图 '{graphName}'。";
        if (nodes.Count == 0) return $"图 '{graphName}' 没有节点。";

        var nodeMap = nodes.ToDictionary(n => n.Guid, n => n.Name);
        var sb = new StringBuilder();
        sb.AppendLine($"图 '{graphName}' 共 {nodes.Count} 个节点、{edges.Count} 条边。");
        sb.AppendLine("节点列表：");
        foreach (var n in nodes) sb.AppendLine($"- {n.Name}");

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

    public Task<string> GetGraphSizeAsync(
        string graphName, CancellationToken ct = default)
        => TrackAsync("GetGraphSize", $"graphName={graphName}",
            () => GetGraphSizeCoreAsync(graphName, ct));

    private async Task<string> GetGraphSizeCoreAsync(string graphName, CancellationToken ct)
    {
        _logger.LogInformation("[Tool] GetGraphSize(graph={Graph})", graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null) return $"未找到图 '{graphName}'。";

        return $"图 '{graphName}' 共 {nodes.Count} 个节点、{edges.Count} 条边。";
    }

    // ══════════════════════════════════════════════════════
    // 工具 5：最短路径
    // ══════════════════════════════════════════════════════

    public Task<string> FindPathAsync(
        string fromNode, string toNode, string graphName, CancellationToken ct = default)
        => TrackAsync("FindPath", $"fromNode={fromNode}, toNode={toNode}, graphName={graphName}",
            () => FindPathCoreAsync(fromNode, toNode, graphName, ct));

    private async Task<string> FindPathCoreAsync(
        string fromNode, string toNode, string graphName, CancellationToken ct)
    {
        _logger.LogInformation(
            "[Tool] FindPath(from={From}, to={To}, graph={Graph})", fromNode, toNode, graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null) return $"未找到图 '{graphName}'。";

        // ★ 宽松解析
        var start = ResolveNode(nodes, fromNode, _logger);
        if (start is null) return $"未找到起始节点 '{fromNode}'。";
        var end = ResolveNode(nodes, toNode, _logger);
        if (end is null) return $"未找到目标节点 '{toNode}'。";

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
                return $"'{start.Name}' 到 '{end.Name}' 的最短路径（{path.Count - 1} 步）：\n" +
                       string.Join(" → ", names);
            }

            foreach (var next in adjacency[last])
            {
                if (visited.Add(next))
                    queue.Enqueue(new List<Guid>(path) { next });
            }
        }
        return $"'{start.Name}' 和 '{end.Name}' 之间不存在路径。";
    }

    // ══════════════════════════════════════════════════════
    // 工具 6：关键词搜索节点
    // ══════════════════════════════════════════════════════

    public Task<string> SearchNodesAsync(
        string keyword, string graphName, CancellationToken ct = default)
        => TrackAsync("SearchNodes", $"keyword={keyword}, graphName={graphName}",
            () => SearchNodesCoreAsync(keyword, graphName, ct));

    private async Task<string> SearchNodesCoreAsync(
        string keyword, string graphName, CancellationToken ct)
    {
        _logger.LogInformation(
            "[Tool] SearchNodes(keyword={Keyword}, graph={Graph})", keyword, graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null) return $"未找到图 '{graphName}'。";
        if (string.IsNullOrWhiteSpace(keyword)) return "关键词不能为空。";

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
        foreach (var m in matches) sb.AppendLine($"- {m.Name}");

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
    // 工具 7：N 跳子图探索
    // ══════════════════════════════════════════════════════

    public Task<string> GetSubgraphAsync(
        string startNode, int hops, string graphName, CancellationToken ct = default)
        => TrackAsync("GetSubgraph", $"startNode={startNode}, hops={hops}, graphName={graphName}",
            () => GetSubgraphCoreAsync(startNode, hops, graphName, ct));

    private async Task<string> GetSubgraphCoreAsync(
        string startNode, int hops, string graphName, CancellationToken ct)
    {
        var safeHops = Math.Clamp(hops, 1, 3);

        _logger.LogInformation(
            "[Tool] GetSubgraph(start={Start}, hops={Hops}, graph={Graph})",
            startNode, safeHops, graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null) return $"未找到图 '{graphName}'。";

        // ★ 宽松解析
        var start = ResolveNode(nodes, startNode, _logger);
        if (start is null) return $"未找到起始节点 '{startNode}'。";

        var adjacency = nodes.ToDictionary(n => n.Guid, _ => new List<Guid>());
        foreach (var e in edges)
        {
            if (adjacency.ContainsKey(e.From)) adjacency[e.From].Add(e.To);
            if (adjacency.ContainsKey(e.To)) adjacency[e.To].Add(e.From);
        }

        var visited = new HashSet<Guid> { start.Guid };
        var currentLevel = new List<Guid> { start.Guid };

        for (int h = 0; h < safeHops; h++)
        {
            var nextLevel = new List<Guid>();
            foreach (var guid in currentLevel)
            {
                foreach (var next in adjacency[guid])
                {
                    if (visited.Add(next)) nextLevel.Add(next);
                }
            }
            currentLevel = nextLevel;
            if (currentLevel.Count == 0) break;
        }

        var nodeMap = nodes.ToDictionary(n => n.Guid, n => n.Name);
        var subgraphNodes = nodes.Where(n => visited.Contains(n.Guid)).ToList();
        var subgraphEdges = edges
            .Where(e => visited.Contains(e.From) && visited.Contains(e.To)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"从 '{start.Name}' 出发 {safeHops} 跳内的子图：");
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
    // 工具 8：节点的出边和入边
    // ══════════════════════════════════════════════════════

    public Task<string> GetEdgesOfNodeAsync(
        string nodeName, string graphName, CancellationToken ct = default)
        => TrackAsync("GetEdgesOfNode", $"nodeName={nodeName}, graphName={graphName}",
            () => GetEdgesOfNodeCoreAsync(nodeName, graphName, ct));

    private async Task<string> GetEdgesOfNodeCoreAsync(
        string nodeName, string graphName, CancellationToken ct)
    {
        _logger.LogInformation(
            "[Tool] GetEdgesOfNode(node={Node}, graph={Graph})", nodeName, graphName);

        var (graph, nodes, edges) = await LoadGraphAsync(graphName, ct);
        if (graph is null) return $"未找到图 '{graphName}'。";

        // ★ 宽松解析
        var target = ResolveNode(nodes, nodeName, _logger);
        if (target is null) return $"在图 '{graphName}' 中未找到节点 '{nodeName}'。";

        var nodeMap = nodes.ToDictionary(n => n.Guid, n => n.Name);
        var outEdges = new List<string>();
        var inEdges = new List<string>();

        foreach (var e in edges)
        {
            if (e.From == target.Guid && nodeMap.TryGetValue(e.To, out var toName))
                outEdges.Add($"- {target.Name} --[{e.Name}]--> {toName}");
            if (e.To == target.Guid && nodeMap.TryGetValue(e.From, out var fromName))
                inEdges.Add($"- {fromName} --[{e.Name}]--> {target.Name}");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"节点 '{target.Name}' 的出边和入边：");

        if (outEdges.Count > 0)
        {
            sb.AppendLine($"出边（{outEdges.Count} 条）：");
            foreach (var line in outEdges) sb.AppendLine(line);
        }
        else sb.AppendLine("出边：无");

        if (inEdges.Count > 0)
        {
            sb.AppendLine($"入边（{inEdges.Count} 条）：");
            foreach (var line in inEdges) sb.AppendLine(line);
        }
        else sb.AppendLine("入边：无");

        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════
    // ★ 节点名宽松解析（核心新增）
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 宽松解析节点名：容忍 LLM 提取参数时的小抖动。
    /// 优先级：
    ///   1. 精确匹配（忽略大小写）
    ///   2. 去掉空格后精确匹配
    ///   3. 去掉常见前缀（根节点、子节点、节点等）后匹配
    ///   4. 加回常见前缀后匹配
    ///   5. 包含匹配（取最长的）
    /// </summary>
    private static NodeMeta? ResolveNode(
        List<NodeMeta> nodes, string inputName, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(inputName) || nodes.Count == 0)
            return null;

        var trimmed = inputName.Trim();

        // 1. 精确匹配
        var exact = nodes.FirstOrDefault(n =>
            n.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        // 2. 去掉空格后精确匹配（半角 + 全角）
        var noSpace = trimmed.Replace(" ", "").Replace("　", "");
        var noSpaceMatch = nodes.FirstOrDefault(n =>
            n.Name.Replace(" ", "").Replace("　", "")
                .Equals(noSpace, StringComparison.OrdinalIgnoreCase));
        if (noSpaceMatch is not null)
        {
            logger.LogInformation("节点名宽松解析（空格）：'{Input}' → '{Actual}'",
                trimmed, noSpaceMatch.Name);
            return noSpaceMatch;
        }

        // 3. 去掉常见前缀后重试（长的放前面）
        var prefixes = new[] { "根节点", "子节点", "父节点", "节点", "Node ", "Node" };
        foreach (var prefix in prefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var stripped = trimmed[prefix.Length..].Trim();
                if (!string.IsNullOrEmpty(stripped))
                {
                    var match = nodes.FirstOrDefault(n =>
                        n.Name.Equals(stripped, StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                    {
                        logger.LogInformation("节点名宽松解析（去前缀）：'{Input}' → '{Actual}'",
                            trimmed, match.Name);
                        return match;
                    }
                }
            }
        }

        // 4. 加回常见前缀后重试
        foreach (var prefix in prefixes)
        {
            var candidate = prefix + trimmed;
            var match = nodes.FirstOrDefault(n =>
                n.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                logger.LogInformation("节点名宽松解析（加前缀）：'{Input}' → '{Actual}'",
                    trimmed, match.Name);
                return match;
            }
        }

        // 5. 包含匹配（取最长匹配，避免歧义）
        var containsMatches = nodes
            .Where(n => n.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                     || trimmed.Contains(n.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n.Name.Length)
            .ToList();

        if (containsMatches.Count > 0)
        {
            var best = containsMatches[0];
            logger.LogInformation("节点名宽松解析（包含）：'{Input}' → '{Actual}'",
                trimmed, best.Name);
            return best;
        }

        logger.LogWarning("节点名宽松解析失败：'{Input}' 无法匹配到图中任何节点", trimmed);
        return null;
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
        if (!root.TryGetProperty("Objects", out var objs) || objs.ValueKind != JsonValueKind.Array)
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
        if (!json.Value.TryGetProperty("Objects", out var objs) || objs.ValueKind != JsonValueKind.Array)
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
        if (!json.Value.TryGetProperty("Objects", out var objs) || objs.ValueKind != JsonValueKind.Array)
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
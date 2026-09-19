using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LiteGraph.Sdk;

namespace DiberyBlazorWebSky.Services;

/// <summary>
/// 根据用户问题动态构建图上下文。
/// 
/// 小图（≤ PrefilterThreshold）：意图识别 + 全量上下文
/// 大图（> PrefilterThreshold）：Embedding 语义预筛选 + 1 跳扩展
/// </summary>
public class GraphDynamicContextService
{
    private readonly LiteGraphSdk _sdk;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GraphDynamicContextService> _logger;

    private static readonly Guid DefaultTenant = Guid.Empty;
    private const int MaxResults = 1000;

    // ★ 阈值：节点数超过此值启用 embedding 预筛选
    private const int PrefilterThreshold = 80;

    // ★ 语义搜索参数
    private const int SemanticTopK = 15;         // 种子节点数
    private const int SemanticNeighborHops = 1;  // 扩展跳数（当前实现固定 1 跳）

    // ★ Ollama embedding 配置（与 McpGraphExplorer 保持一致）
    private const string OllamaEmbeddingUrl = "http://localhost:11618/api/embeddings";
    private const string EmbeddingModel = "bge-large";

    public GraphDynamicContextService(
        LiteGraphSdk sdk,
        IHttpClientFactory httpClientFactory,
        ILogger<GraphDynamicContextService> logger)
    {
        _sdk = sdk;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// 根据用户问题构建动态上下文。
    /// 返回：(LLM 用的上下文文本, UI 用的短标签)
    /// </summary>
    public async Task<(string Context, string Label)> BuildAsync(
        Guid graphGuid,
        string graphName,
        string userQuestion)
    {
        try
        {
            // 先查节点总数（只查 1 条，拿 TotalRecords 即可，避免全量加载）
            var nodeCount = await GetNodeCountAsync(graphGuid);

            // ─── 小图：走原有意图模式 ───
            if (nodeCount <= PrefilterThreshold)
            {
                var intent = DetectIntent(userQuestion);
                _logger.LogInformation(
                    "小图（{Count} 节点），意图: {Intent}", nodeCount, intent);

                var nodes = await LoadNodesAsync(graphGuid);
                var edges = await LoadEdgesAsync(graphGuid);

                return intent switch
                {
                    "neighbors" => (BuildNeighborsContext(graphName, nodes, edges),
                                    $"{graphName} · 邻居查询"),
                    "relations" => (BuildRelationsContext(graphName, nodes, edges),
                                    $"{graphName} · 关系查询"),
                    "list" => (BuildListContext(graphName, nodes),
                               $"{graphName} · 节点列表"),
                    _ => (BuildFullContext(graphName, nodes, edges),
                          $"{graphName}（{nodes.Count} 节点 / {edges.Count} 边）")
                };
            }

            // ─── 大图：Embedding 语义预筛选 ───
            _logger.LogInformation(
                "大图（{Count} 节点），启用 embedding 预筛选", nodeCount);

            return await BuildSemanticContextAsync(graphGuid, graphName, userQuestion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "构建动态上下文失败");
            return ($"[上下文构建失败: {ex.Message}]", "上下文错误");
        }
    }

    // ══════════════════════════════════════════════════════
    // 意图识别（小图模式）
    // ══════════════════════════════════════════════════════

    private static string DetectIntent(string question)
    {
        // 关系问题优先（更具体）
        if (Regex.IsMatch(question, @"(什么关系|之间|两者|如何.*关|怎么.*关|父|子|上.*下.*关)"))
            return "relations";

        // 邻居
        if (Regex.IsMatch(question, @"(邻居|相邻|邻近|周边|和.*有关|谁.*连|跟.*连|关联)"))
            return "neighbors";

        // 列表
        if (Regex.IsMatch(question, @"(所有节点|有哪些|列出|列表|全部|清单|列举|多少.*节点)"))
            return "list";

        // 路径问题当作关系问题处理
        if (Regex.IsMatch(question, @"(路径|怎么.*到|如何.*到|最短)"))
            return "relations";

        return "full";
    }

    // ══════════════════════════════════════════════════════
    // Embedding 语义预筛选（大图模式）
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 大图上下文构建：
    /// 1. 用户问题 → embedding
    /// 2. 遍历图中所有向量，计算余弦相似度，取 top-K
    /// 3. 从 top-K 节点扩展 N 跳邻居
    /// 4. 只注入相关子图
    /// 若任一步骤失败，降级为全量上下文。
    /// </summary>
    private async Task<(string Context, string Label)> BuildSemanticContextAsync(
        Guid graphGuid,
        string graphName,
        string userQuestion)
    {
        // ─── 1. 问题向量化 ───
        var queryVec = await GetEmbeddingAsync(userQuestion);
        if (queryVec == null || queryVec.Count == 0)
        {
            _logger.LogWarning("Embedding 生成失败，降级为全量上下文");
            return await BuildFallbackFullAsync(graphGuid, graphName);
        }

        _logger.LogInformation("查询向量维度：{Dim}", queryVec.Count);

        // ─── 2. 加载全量图数据 ───
        var nodes = await LoadNodesAsync(graphGuid);
        var edges = await LoadEdgesAsync(graphGuid);

        // ─── 3. 加载图中所有向量 ───
        var vectors = await LoadAllVectorsAsync(graphGuid);
        if (vectors.Count == 0)
        {
            _logger.LogWarning("图中没有向量数据，降级为全量上下文（请先为节点生成向量）");
            return (BuildFullContext(graphName, nodes, edges),
                    $"{graphName}（{nodes.Count} 节点 / {edges.Count} 边，无向量）");
        }

        // ─── 4. 计算相似度，取 top-K ───
        var scored = vectors
            .Where(v => v.NodeGUID.HasValue && v.Vectors != null && v.Vectors.Count > 0)
            .Select(v => (
                NodeGuid: v.NodeGUID!.Value,
                Score: CosineSimilarity(queryVec, v.Vectors!)
            ))
            .OrderByDescending(x => x.Score)
            .Take(SemanticTopK)
            .ToList();

        if (scored.Count == 0)
        {
            _logger.LogWarning("向量匹配为空，降级为全量上下文");
            return (BuildFullContext(graphName, nodes, edges),
                    $"{graphName}（{nodes.Count} 节点 / {edges.Count} 边）");
        }

        // ─── 5. 扩展 1 跳：把种子节点的邻居加入 ───
        var seedGuids = scored.Select(x => x.NodeGuid).ToHashSet();
        var expandedGuids = new HashSet<Guid>(seedGuids);

        foreach (var edge in edges)
        {
            if (seedGuids.Contains(edge.From)) expandedGuids.Add(edge.To);
            if (seedGuids.Contains(edge.To)) expandedGuids.Add(edge.From);
        }

        // ─── 6. 构建子图 ───
        var subsetNodes = nodes
            .Where(n => expandedGuids.Contains(n.GUID))
            .ToList();

        var subsetEdges = edges
            .Where(e => expandedGuids.Contains(e.From) && expandedGuids.Contains(e.To))
            .ToList();

        _logger.LogInformation(
            "预筛选完成：{Total} 节点 → {Seed} 种子 → {Expanded} 相关节点（{Edges} 条边）",
            nodes.Count, seedGuids.Count, subsetNodes.Count, subsetEdges.Count);

        // ─── 7. 生成上下文（用关系模式，提供节点+边）───
        var context = BuildRelationsContext(graphName, subsetNodes, subsetEdges);
        var label = $"{graphName} · 语义检索（{subsetNodes.Count}/{nodes.Count} 节点）";

        return (context, label);
    }

    /// <summary>降级：加载全量数据后走 full 模式。</summary>
    private async Task<(string, string)> BuildFallbackFullAsync(Guid graphGuid, string graphName)
    {
        var nodes = await LoadNodesAsync(graphGuid);
        var edges = await LoadEdgesAsync(graphGuid);
        return (BuildFullContext(graphName, nodes, edges),
                $"{graphName}（{nodes.Count} 节点 / {edges.Count} 边）");
    }

    // ══════════════════════════════════════════════════════
    // Embedding / 相似度
    // ══════════════════════════════════════════════════════

    /// <summary>调用 Ollama 生成 embedding。</summary>
    private async Task<List<float>?> GetEmbeddingAsync(string text)
    {
        try
        {
            var http = _httpClientFactory.CreateClient();
            var response = await http.PostAsJsonAsync(
                OllamaEmbeddingUrl,
                new { model = EmbeddingModel, prompt = text });

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "Ollama 返回 {Status}: {Body}", response.StatusCode, body);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("embedding", out var arr))
            {
                _logger.LogWarning("Ollama 响应中没有 embedding 字段");
                return null;
            }

            return arr.EnumerateArray().Select(x => x.GetSingle()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调用 Ollama embedding 失败");
            return null;
        }
    }

    /// <summary>余弦相似度。</summary>
    private static float CosineSimilarity(List<float> a, List<float> b)
    {
        if (a.Count != b.Count || a.Count == 0) return 0f;

        float dot = 0f, na = 0f, nb = 0f;
        for (int i = 0; i < a.Count; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        if (na == 0f || nb == 0f) return 0f;
        return dot / (MathF.Sqrt(na) * MathF.Sqrt(nb));
    }

    // ══════════════════════════════════════════════════════
    // 数据加载
    // ══════════════════════════════════════════════════════

    /// <summary>只查节点总数（MaxResults=1，避免全量加载）。</summary>
    private async Task<long> GetNodeCountAsync(Guid graphGuid)
    {
        var query = new EnumerationRequest
        {
            TenantGUID = DefaultTenant,
            GraphGUID = graphGuid,
            MaxResults = 1
        };
        var result = await _sdk.Node.Enumerate(query);
        return result.TotalRecords;
    }

    private async Task<List<Node>> LoadNodesAsync(Guid graphGuid)
    {
        var query = new EnumerationRequest
        {
            TenantGUID = DefaultTenant,
            GraphGUID = graphGuid,
            MaxResults = MaxResults
        };
        var result = await _sdk.Node.Enumerate(query);
        return result.Objects ?? new List<Node>();
    }

    private async Task<List<Edge>> LoadEdgesAsync(Guid graphGuid)
    {
        var query = new EnumerationRequest
        {
            TenantGUID = DefaultTenant,
            GraphGUID = graphGuid,
            MaxResults = MaxResults
        };
        var result = await _sdk.Edge.Enumerate(query);
        return result.Objects ?? new List<Edge>();
    }

    private async Task<List<VectorMetadata>> LoadAllVectorsAsync(Guid graphGuid)
    {
        var query = new EnumerationRequest
        {
            TenantGUID = DefaultTenant,
            GraphGUID = graphGuid,
            Ordering = EnumerationOrderEnum.CreatedDescending,
            MaxResults = MaxResults
        };
        var result = await _sdk.Vector.Enumerate(query);
        return result.Objects ?? new List<VectorMetadata>();
    }

    // ══════════════════════════════════════════════════════
    // 上下文文本构造
    // ══════════════════════════════════════════════════════

    private static string BuildNeighborsContext(
        string graphName, List<Node> nodes, List<Edge> edges)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<graph_context name=\"{graphName}\" mode=\"neighbors\">");
        sb.AppendLine("<description>这是当前图的节点及其邻居关系。");
        sb.AppendLine("邻居定义：一个节点通过任一条边（出边或入边）直接相连的其他节点。");
        sb.AppendLine("也就是说，只要两个节点之间有边（无论方向），它们就互为邻居。</description>");

        var nodeMap = nodes.ToDictionary(n => n.GUID, n => n.Name);

        var adjacency = nodes.ToDictionary(
            n => n.GUID,
            n => new List<(string direction, string otherName, string edgeName)>());

        foreach (var e in edges)
        {
            if (!nodeMap.TryGetValue(e.From, out var fromName)) continue;
            if (!nodeMap.TryGetValue(e.To, out var toName)) continue;

            adjacency[e.From].Add(("出", toName, e.Name));
            adjacency[e.To].Add(("入", fromName, e.Name));
        }

        sb.AppendLine("<nodes>");
        foreach (var n in nodes)
            sb.AppendLine($"- {n.Name}");
        sb.AppendLine("</nodes>");

        sb.AppendLine("<adjacency>");
        foreach (var n in nodes)
        {
            var neighbors = adjacency[n.GUID];
            if (neighbors.Count == 0)
            {
                sb.AppendLine($"{n.Name} 的邻居：(无)");
            }
            else
            {
                sb.AppendLine($"{n.Name} 的邻居：");
                foreach (var (direction, otherName, edgeName) in neighbors)
                {
                    sb.AppendLine($"  - {direction}：{otherName}（通过 {edgeName}）");
                }
            }
        }
        sb.AppendLine("</adjacency>");

        sb.AppendLine("<edges>");
        if (edges.Count == 0)
            sb.AppendLine("(图中没有边)");
        else
            foreach (var e in edges)
            {
                var fromName = nodeMap.TryGetValue(e.From, out var f) ? f : e.From.ToString();
                var toName = nodeMap.TryGetValue(e.To, out var t) ? t : e.To.ToString();
                sb.AppendLine($"- {fromName} --[{e.Name}]--> {toName}");
            }
        sb.AppendLine("</edges>");
        sb.AppendLine("</graph_context>");
        return sb.ToString();
    }

    private static string BuildRelationsContext(
        string graphName, List<Node> nodes, List<Edge> edges)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<graph_context name=\"{graphName}\" mode=\"relations\">");
        sb.AppendLine("<description>这是当前图的所有节点和边，用于回答节点关系问题。");
        sb.AppendLine("边的格式是：源节点 --[关系名]--> 目标节点");
        sb.AppendLine("例如：\"A --[PARENT_OF]--> B\" 表示 A 是 B 的父节点（A → B 方向）。");
        sb.AppendLine("回答关系问题时，请明确指出关系的方向和角色。例如问\"A 和 B 是什么关系\"，应回答\"A 是 B 的父节点\"，而不仅仅说\"关系是 PARENT_OF\"。</description>");

        var nodeMap = nodes.ToDictionary(n => n.GUID, n => n.Name);

        sb.AppendLine("<nodes>");
        foreach (var n in nodes)
            sb.AppendLine($"- {n.Name}");
        sb.AppendLine("</nodes>");

        sb.AppendLine("<edges>");
        if (edges.Count == 0)
            sb.AppendLine("(图中没有边)");
        else
            foreach (var e in edges)
            {
                var fromName = nodeMap.TryGetValue(e.From, out var f) ? f : e.From.ToString();
                var toName = nodeMap.TryGetValue(e.To, out var t) ? t : e.To.ToString();
                sb.AppendLine($"- {fromName} --[{e.Name}]--> {toName}");
            }
        sb.AppendLine("</edges>");
        sb.AppendLine("</graph_context>");
        return sb.ToString();
    }

    private static string BuildListContext(string graphName, List<Node> nodes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<graph_context name=\"{graphName}\" mode=\"list\">");
        sb.AppendLine("<description>这是当前图的节点列表。</description>");
        sb.AppendLine("<nodes>");
        foreach (var n in nodes)
            sb.AppendLine($"- {n.Name}");
        sb.AppendLine("</nodes>");
        sb.AppendLine("</graph_context>");
        return sb.ToString();
    }

    private static string BuildFullContext(
        string graphName, List<Node> nodes, List<Edge> edges)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<graph_context name=\"{graphName}\">");

        var nodeMap = nodes.ToDictionary(n => n.GUID, n => n.Name);

        sb.AppendLine("<nodes>");
        if (nodes.Count == 0)
            sb.AppendLine("(无节点)");
        else
            foreach (var n in nodes)
                sb.AppendLine($"- {n.Name} (GUID: {n.GUID})");
        sb.AppendLine("</nodes>");

        sb.AppendLine("<edges>");
        if (edges.Count == 0)
            sb.AppendLine("(无边)");
        else
            foreach (var e in edges)
            {
                var fromName = nodeMap.TryGetValue(e.From, out var f) ? f : e.From.ToString();
                var toName = nodeMap.TryGetValue(e.To, out var t) ? t : e.To.ToString();
                sb.AppendLine($"- {fromName} --[{e.Name}]--> {toName}");
            }
        sb.AppendLine("</edges>");
        sb.AppendLine("</graph_context>");
        return sb.ToString();
    }
}
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DiberyBlazorWebSky.Models.Graph;

namespace DiberyBlazorWebSky.Services;

/// <summary>
/// 根据用户问题动态构建图上下文。
/// 小图（≤ PrefilterThreshold）：意图识别 + 全量上下文
/// 大图（> PrefilterThreshold）：Embedding 语义预筛选 + 1 跳扩展
/// 所有 LiteGraph 访问统一走 GraphApiClient（HTTP → MAFWorkFlowApi）。
/// </summary>
public class GraphDynamicContextService
{
    private readonly GraphApiClient _graphApi;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GraphDynamicContextService> _logger;

    private const int MaxResults = 1000;
    private const int PrefilterThreshold = 80;
    private const int SemanticTopK = 15;
    private const int SemanticNeighborHops = 1;

    private const string OllamaEmbeddingUrl = "http://localhost:11618/api/embeddings";
    private const string EmbeddingModel = "bge-large";

    public GraphDynamicContextService(
        GraphApiClient graphApi,
        IHttpClientFactory httpClientFactory,
        ILogger<GraphDynamicContextService> logger)
    {
        _graphApi = graphApi;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<(string Context, string Label)> BuildAsync(
        Guid graphGuid, string graphName, string userQuestion)
    {
        try
        {
            var nodeCount = await GetNodeCountAsync(graphGuid);

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
                    "list"      => (BuildListContext(graphName, nodes),
                                    $"{graphName} · 节点列表"),
                    _           => (BuildFullContext(graphName, nodes, edges),
                                    $"{graphName}（{nodes.Count} 节点 / {edges.Count} 边）")
                };
            }

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
    // 意图识别（不变）
    // ══════════════════════════════════════════════════════
    private static string DetectIntent(string question)
    {
        if (Regex.IsMatch(question, @"(什么关系|之间|两者|如何.*关|怎么.*关|父|子|上.*下.*关)"))
            return "relations";
        if (Regex.IsMatch(question, @"(邻居|相邻|邻近|周边|和.*有关|谁.*连|跟.*连|关联)"))
            return "neighbors";
        if (Regex.IsMatch(question, @"(所有节点|有哪些|列出|列表|全部|清单|列举|多少.*节点)"))
            return "list";
        if (Regex.IsMatch(question, @"(路径|怎么.*到|如何.*到|最短)"))
            return "relations";
        return "full";
    }

    // ══════════════════════════════════════════════════════
    // Embedding 语义预筛选（大图模式）
    // ══════════════════════════════════════════════════════
    private async Task<(string Context, string Label)> BuildSemanticContextAsync(
        Guid graphGuid, string graphName, string userQuestion)
    {
        var queryVec = await GetEmbeddingAsync(userQuestion);
        if (queryVec == null || queryVec.Count == 0)
        {
            _logger.LogWarning("Embedding 生成失败，降级为全量上下文");
            return await BuildFallbackFullAsync(graphGuid, graphName);
        }

        _logger.LogInformation("查询向量维度：{Dim}", queryVec.Count);

        var nodes = await LoadNodesAsync(graphGuid);
        var edges = await LoadEdgesAsync(graphGuid);
        var vectors = await LoadAllVectorsAsync(graphGuid);

        if (vectors.Count == 0)
        {
            _logger.LogWarning("图中没有向量数据，降级为全量上下文");
            return (BuildFullContext(graphName, nodes, edges),
                    $"{graphName}（{nodes.Count} 节点 / {edges.Count} 边，无向量）");
        }

        var scored = vectors
            .Where(v => v.NodeGUID.HasValue && v.Vectors is { Count: > 0 })
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

        var seedGuids = scored.Select(x => x.NodeGuid).ToHashSet();
        var expandedGuids = new HashSet<Guid>(seedGuids);

        foreach (var edge in edges)
        {
            if (seedGuids.Contains(edge.From)) expandedGuids.Add(edge.To);
            if (seedGuids.Contains(edge.To))   expandedGuids.Add(edge.From);
        }

        var subsetNodes = nodes.Where(n => expandedGuids.Contains(n.GUID)).ToList();
        var subsetEdges = edges
            .Where(e => expandedGuids.Contains(e.From) && expandedGuids.Contains(e.To))
            .ToList();

        _logger.LogInformation(
            "预筛选完成：{Total} 节点 → {Seed} 种子 → {Expanded} 相关节点（{Edges} 条边）",
            nodes.Count, seedGuids.Count, subsetNodes.Count, subsetEdges.Count);

        var context = BuildRelationsContext(graphName, subsetNodes, subsetEdges);
        var label = $"{graphName} · 语义检索（{subsetNodes.Count}/{nodes.Count} 节点）";
        return (context, label);
    }

    private async Task<(string, string)> BuildFallbackFullAsync(Guid graphGuid, string graphName)
    {
        var nodes = await LoadNodesAsync(graphGuid);
        var edges = await LoadEdgesAsync(graphGuid);
        return (BuildFullContext(graphName, nodes, edges),
                $"{graphName}（{nodes.Count} 节点 / {edges.Count} 边）");
    }

    // ══════════════════════════════════════════════════════
    // Embedding / 相似度（不变）
    // ══════════════════════════════════════════════════════
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
                _logger.LogWarning("Ollama 返回 {Status}: {Body}", response.StatusCode, body);
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

    private static float CosineSimilarity(List<float> a, List<float> b)
    {
        if (a.Count != b.Count || a.Count == 0) return 0f;
        float dot = 0f, na = 0f, nb = 0f;
        for (int i = 0; i < a.Count; i++)
        {
            dot += a[i] * b[i];
            na  += a[i] * a[i];
            nb  += b[i] * b[i];
        }
        if (na == 0f || nb == 0f) return 0f;
        return dot / (MathF.Sqrt(na) * MathF.Sqrt(nb));
    }

    // ══════════════════════════════════════════════════════
    // 数据加载 —— 全部走 GraphApiClient
    // ══════════════════════════════════════════════════════
    private async Task<long> GetNodeCountAsync(Guid graphGuid)
    {
        var resp = await _graphApi.EnumerateNodesAsync(graphGuid, maxResults: 1);
        return resp?.TotalRecords ?? 0;
    }

    private async Task<List<GraphNodeDto>> LoadNodesAsync(Guid graphGuid)
    {
        var resp = await _graphApi.EnumerateNodesAsync(graphGuid, maxResults: MaxResults);
        return resp?.Objects ?? new List<GraphNodeDto>();
    }

    private async Task<List<GraphEdgeDto>> LoadEdgesAsync(Guid graphGuid)
    {
        var resp = await _graphApi.EnumerateEdgesAsync(graphGuid, maxResults: MaxResults);
        return resp?.Objects ?? new List<GraphEdgeDto>();
    }

    private async Task<List<GraphVectorDto>> LoadAllVectorsAsync(Guid graphGuid)
    {
        var resp = await _graphApi.EnumerateVectorsAsync(graphGuid, maxResults: MaxResults);
        return resp?.Objects ?? new List<GraphVectorDto>();
    }

    // ══════════════════════════════════════════════════════
    // 上下文文本构造（把 Node/Edge 换成 DTO）
    // ══════════════════════════════════════════════════════
    private static string BuildNeighborsContext(
        string graphName, List<GraphNodeDto> nodes, List<GraphEdgeDto> edges)
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
            if (!nodeMap.TryGetValue(e.To,   out var toName))   continue;

            adjacency[e.From].Add(("出", toName,   e.Name));
            adjacency[e.To].Add(  ("入", fromName, e.Name));
        }

        sb.AppendLine("<nodes>");
        foreach (var n in nodes) sb.AppendLine($"- {n.Name}");
        sb.AppendLine("</nodes>");

        sb.AppendLine("<adjacency>");
        foreach (var n in nodes)
        {
            var neighbors = adjacency[n.GUID];
            if (neighbors.Count == 0) sb.AppendLine($"{n.Name} 的邻居：(无)");
            else
            {
                sb.AppendLine($"{n.Name} 的邻居：");
                foreach (var (direction, otherName, edgeName) in neighbors)
                    sb.AppendLine($"  - {direction}：{otherName}（通过 {edgeName}）");
            }
        }
        sb.AppendLine("</adjacency>");

        sb.AppendLine("<edges>");
        if (edges.Count == 0) sb.AppendLine("(图中没有边)");
        else foreach (var e in edges)
        {
            var f = nodeMap.TryGetValue(e.From, out var fn) ? fn : e.From.ToString();
            var t = nodeMap.TryGetValue(e.To,   out var tn) ? tn : e.To.ToString();
            sb.AppendLine($"- {f} --[{e.Name}]--> {t}");
        }
        sb.AppendLine("</edges>");
        sb.AppendLine("</graph_context>");
        return sb.ToString();
    }

    private static string BuildRelationsContext(
        string graphName, List<GraphNodeDto> nodes, List<GraphEdgeDto> edges)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<graph_context name=\"{graphName}\" mode=\"relations\">");
        sb.AppendLine("<description>这是当前图的所有节点和边，用于回答节点关系问题。");
        sb.AppendLine("边的格式是：源节点 --[关系名]--> 目标节点");
        sb.AppendLine("例如：\"A --[PARENT_OF]--> B\" 表示 A 是 B 的父节点（A → B 方向）。");
        sb.AppendLine("回答关系问题时，请明确指出关系的方向和角色。</description>");

        var nodeMap = nodes.ToDictionary(n => n.GUID, n => n.Name);

        sb.AppendLine("<nodes>");
        foreach (var n in nodes) sb.AppendLine($"- {n.Name}");
        sb.AppendLine("</nodes>");

        sb.AppendLine("<edges>");
        if (edges.Count == 0) sb.AppendLine("(图中没有边)");
        else foreach (var e in edges)
        {
            var f = nodeMap.TryGetValue(e.From, out var fn) ? fn : e.From.ToString();
            var t = nodeMap.TryGetValue(e.To,   out var tn) ? tn : e.To.ToString();
            sb.AppendLine($"- {f} --[{e.Name}]--> {t}");
        }
        sb.AppendLine("</edges>");
        sb.AppendLine("</graph_context>");
        return sb.ToString();
    }

    private static string BuildListContext(string graphName, List<GraphNodeDto> nodes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<graph_context name=\"{graphName}\" mode=\"list\">");
        sb.AppendLine("<description>这是当前图的节点列表。</description>");
        sb.AppendLine("<nodes>");
        foreach (var n in nodes) sb.AppendLine($"- {n.Name}");
        sb.AppendLine("</nodes>");
        sb.AppendLine("</graph_context>");
        return sb.ToString();
    }

    private static string BuildFullContext(
        string graphName, List<GraphNodeDto> nodes, List<GraphEdgeDto> edges)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<graph_context name=\"{graphName}\">");

        var nodeMap = nodes.ToDictionary(n => n.GUID, n => n.Name);

        sb.AppendLine("<nodes>");
        if (nodes.Count == 0) sb.AppendLine("(无节点)");
        else foreach (var n in nodes)
            sb.AppendLine($"- {n.Name} (GUID: {n.GUID})");
        sb.AppendLine("</nodes>");

        sb.AppendLine("<edges>");
        if (edges.Count == 0) sb.AppendLine("(无边)");
        else foreach (var e in edges)
        {
            var f = nodeMap.TryGetValue(e.From, out var fn) ? fn : e.From.ToString();
            var t = nodeMap.TryGetValue(e.To,   out var tn) ? tn : e.To.ToString();
            sb.AppendLine($"- {f} --[{e.Name}]--> {t}");
        }
        sb.AppendLine("</edges>");
        sb.AppendLine("</graph_context>");
        return sb.ToString();
    }
}
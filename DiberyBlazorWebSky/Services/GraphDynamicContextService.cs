using System.Text;
using System.Text.RegularExpressions;
using LiteGraph.Sdk;

namespace DiberyBlazorWebSky.Services;

/// <summary>
/// 根据用户问题动态构建图上下文。
/// 通过意图识别 + LiteGraph SDK 查询，为 LLM 提供精准的图数据。
/// </summary>
public class GraphDynamicContextService
{
    private readonly LiteGraphSdk _sdk;
    private readonly ILogger<GraphDynamicContextService> _logger;

    private static readonly Guid DefaultTenant = Guid.Empty;
    private const int MaxResults = 1000;

    public GraphDynamicContextService(
        LiteGraphSdk sdk,
        ILogger<GraphDynamicContextService> logger)
    {
        _sdk = sdk;
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
            var intent = DetectIntent(userQuestion);
            _logger.LogInformation("图查询意图: {Intent}", intent);

            // 一次加载节点和边，多处复用
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "构建动态上下文失败");
            return ($"[上下文构建失败: {ex.Message}]", "上下文错误");
        }
    }

    /// <summary>
    /// 基于关键词的意图识别。
    /// </summary>
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

        // 路径问题当作关系问题处理（图通常小，边信息足够 LLM 推理）
        if (Regex.IsMatch(question, @"(路径|怎么.*到|如何.*到|最短)"))
            return "relations";

        return "full";
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

    // ─── 各意图的上下文构造 ──────────────────────────

    /// <summary>
    /// 邻居查询：主动构建邻接表，明确标注每个节点的出/入邻居。
    /// </summary>
    private static string BuildNeighborsContext(
        string graphName, List<Node> nodes, List<Edge> edges)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<graph_context name=\"{graphName}\" mode=\"neighbors\">");
        sb.AppendLine("<description>这是当前图的节点及其邻居关系。");
        sb.AppendLine("邻居定义：一个节点通过任一条边（出边或入边）直接相连的其他节点。");
        sb.AppendLine("也就是说，只要两个节点之间有边（无论方向），它们就互为邻居。</description>");

        var nodeMap = nodes.ToDictionary(n => n.GUID, n => n.Name);

        // 构建邻接表：每个节点的出/入邻居
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

        // ★ 关键：直接给出邻接表，LLM 无需自己推导
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
        {
            sb.AppendLine("(图中没有边)");
        }
        else
        {
            foreach (var e in edges)
            {
                var fromName = nodeMap.TryGetValue(e.From, out var f) ? f : e.From.ToString();
                var toName = nodeMap.TryGetValue(e.To, out var t) ? t : e.To.ToString();
                sb.AppendLine($"- {fromName} --[{e.Name}]--> {toName}");
            }
        }
        sb.AppendLine("</edges>");
        sb.AppendLine("</graph_context>");
        return sb.ToString();
    }

    /// <summary>
    /// 关系查询：强调边的方向，给出明确示例。
    /// </summary>
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
        {
            sb.AppendLine("(图中没有边)");
        }
        else
        {
            foreach (var e in edges)
            {
                var fromName = nodeMap.TryGetValue(e.From, out var f) ? f : e.From.ToString();
                var toName = nodeMap.TryGetValue(e.To, out var t) ? t : e.To.ToString();
                sb.AppendLine($"- {fromName} --[{e.Name}]--> {toName}");
            }
        }
        sb.AppendLine("</edges>");
        sb.AppendLine("</graph_context>");
        return sb.ToString();
    }

    /// <summary>
    /// 列表查询：只给节点名列表。
    /// </summary>
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

    /// <summary>
    /// 全量查询（兜底）：所有节点 + 所有边。
    /// </summary>
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
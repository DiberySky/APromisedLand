using MAFWorkFlowApi.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 用 MAF 封装的图 Agent：
/// - chatClient.AsAIAgent 创建单个 AIAgent
/// - 通过 tools 传入 AIFunction 列表（LLM 自主调用）
/// - 通过 AgentSessionStore (Redis) 持久化多轮会话
/// - 通过 Scoped ToolCallContext 记录工具调用
/// - 输出 sanitize：剥除 LLM 偶尔泄漏的 tool_call 标记
/// </summary>
public sealed class GraphAgentService
{
    private readonly AIAgent _graphAgent;
    private readonly AgentSessionStore _sessionStore;
    private readonly IConversationCatalog _catalog;
    private readonly ToolCallContext _toolCtx;
    private readonly ILogger<GraphAgentService> _logger;

    public GraphAgentService(
        [FromKeyedServices("chat-model")] IChatClient chatClient,
        GraphTools graphTools,
        AgentSessionStore sessionStore,
        IConversationCatalog catalog,
        ToolCallContext toolCtx,
        ILoggerFactory loggerFactory)
    {
        _sessionStore = sessionStore;
        _catalog = catalog;
        _toolCtx = toolCtx;
        _logger = loggerFactory.CreateLogger<GraphAgentService>();

        var tools = BuildTools(graphTools);

        _graphAgent = chatClient.AsAIAgent(
            instructions: BuildInstructions(),
            name: "GraphAssistant",
            tools: tools);
    }

    // ══════════════════════════════════════════════════════
    // 对外：单轮/多轮对话
    // ══════════════════════════════════════════════════════

    public async Task<AgentReply> ChatAsync(
        string? conversationId,
        string userMessage,
        CancellationToken ct)
    {
        var currentConversationId = conversationId ?? Guid.NewGuid().ToString("N");

        // ★ 重置工具调用记录
        _toolCtx.Reset();

        var session = await _sessionStore.GetSessionAsync(
            _graphAgent, currentConversationId, ct)
            ?? await _graphAgent.CreateSessionAsync(cancellationToken: ct);

        var response = await _graphAgent.RunAsync(
            userMessage, session, cancellationToken: ct);

        await _sessionStore.SaveSessionAsync(
            _graphAgent, currentConversationId, session, ct);

        // ★ 读取本次调用的工具列表
        var toolsInvoked = _toolCtx.InvokedTools.ToList();

        // ★ 输出 sanitize
        var finalReply = SanitizeReply(response, toolsInvoked);

        return new AgentReply(
            ConversationId: currentConversationId,
            AgentName: _graphAgent.Name ?? "GraphAssistant",
            Reply: finalReply,
            MessageCount: response.Messages.Count,
            ToolsInvoked: toolsInvoked);
    }

    public ValueTask<IReadOnlyList<string>> ListConversationsAsync(CancellationToken ct)
        => _catalog.ListConversationIdsAsync(ct);

    public ValueTask<bool> ResetConversationAsync(string conversationId, CancellationToken ct)
        => _catalog.DeleteConversationAsync(conversationId, ct);

    public ValueTask<IReadOnlyList<SessionMessage>> GetSessionMessagesAsync(
        string conversationId, CancellationToken ct)
        => _catalog.GetSessionMessagesAsync(conversationId, ct);

    // ══════════════════════════════════════════════════════
    // 输出清理
    // ══════════════════════════════════════════════════════

    private string SanitizeReply(AgentResponse response, List<string> toolsInvoked)
    {
        var rawText = response.Text;

        if (!string.IsNullOrEmpty(rawText) && !ReplySanitizer.ContainsToolCallMarkers(rawText))
            return rawText.Trim();

        _logger.LogWarning(
            "检测到 tool_call 残留标记或无文本，尝试从消息历史提取最终回答");

        var fallback = response.Messages
            .Where(m => m.Role == ChatRole.Assistant)
            .Select(m => m.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t) && !ReplySanitizer.ContainsToolCallMarkers(t))
            .LastOrDefault();

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            _logger.LogInformation("成功从消息历史提取最终回答");
            return fallback.Trim();
        }

        if (toolsInvoked.Count > 0)
        {
            _logger.LogWarning(
                "LLM 未生成最终回答，已调用工具：{Tools}",
                string.Join(", ", toolsInvoked));

            return $"抱歉，我在调用工具 {string.Join("、", toolsInvoked)} 后" +
                   "没能生成最终回答。请换一种问法，或者稍后重试。";
        }

        return "抱歉，我无法完成这个请求。请换个方式提问，或者确认图名是否正确。";
    }

    // ══════════════════════════════════════════════════════
    // 私有：构建工具列表
    // ══════════════════════════════════════════════════════

    private static IList<AITool> BuildTools(GraphTools t)
    {
        return new List<AITool>
        {
            // ─── 原有 5 个工具 ───────────────────────────
            AIFunctionFactory.Create(
                (Func<string, string, CancellationToken, Task<string>>)
                ((nodeName, graphName, ct) => t.GetNeighborsAsync(nodeName, graphName, ct)),
                name: "GetNeighbors",
                description: "获取指定节点的所有邻居节点（出边+入边）。当用户问'X 有哪些邻居'、'X 和谁相连'时使用。"),

            AIFunctionFactory.Create(
                (Func<string, string, string, CancellationToken, Task<string>>)
                ((nodeA, nodeB, graphName, ct) => t.GetRelationsAsync(nodeA, nodeB, graphName, ct)),
                name: "GetRelations",
                description: "查询两个指定节点之间的直接关系（边）。当用户问'A 和 B 是什么关系'时使用。"),

            AIFunctionFactory.Create(
                (Func<string, CancellationToken, Task<string>>)
                ((graphName, ct) => t.ListAllNodesAsync(graphName, ct)),
                name: "ListAllNodes",
                description: "列出图中所有节点和边。当用户问'有哪些节点'、'列出全部'时使用。"),

            AIFunctionFactory.Create(
                (Func<string, CancellationToken, Task<string>>)
                ((graphName, ct) => t.GetGraphSizeAsync(graphName, ct)),
                name: "GetGraphSize",
                description: "获取图的规模（节点数+边数）。当用户问'图有多大'、'有多少节点'时使用。"),

            AIFunctionFactory.Create(
                (Func<string, string, string, CancellationToken, Task<string>>)
                ((fromNode, toNode, graphName, ct) => t.FindPathAsync(fromNode, toNode, graphName, ct)),
                name: "FindPath",
                description: "查找两个节点之间的最短路径。当用户问'从 A 到 B 怎么走'、'A 到 B 的路径'时使用。"),

            // ─── ★ 新增 3 个工具 ──────────────────────────
            AIFunctionFactory.Create(
                (Func<string, string, CancellationToken, Task<string>>)
                ((keyword, graphName, ct) => t.SearchNodesAsync(keyword, graphName, ct)),
                name: "SearchNodes",
                description: "按关键词搜索名称包含该关键词的节点（不区分大小写、部分匹配）。" +
                             "当用户不确定节点的完整名称，或想找'和 XX 相关的节点'、'有哪些 XX'时使用。"),

            AIFunctionFactory.Create(
                (Func<string, int, string, CancellationToken, Task<string>>)
                ((startNode, hops, graphName, ct) => t.GetSubgraphAsync(startNode, hops, graphName, ct)),
                name: "GetSubgraph",
                description: "从起始节点出发，返回指定跳数内（1-3 跳）的所有节点和边，用于探索图结构。" +
                             "当用户想了解'X 附近有什么'、'从 X 出发 N 步能到哪些节点'时使用。"),

            AIFunctionFactory.Create(
                (Func<string, string, CancellationToken, Task<string>>)
                ((nodeName, graphName, ct) => t.GetEdgesOfNodeAsync(nodeName, graphName, ct)),
                name: "GetEdgesOfNode",
                description: "查询指定节点的所有出边和入边，明确区分边的方向。" +
                             "当用户问'X 指向谁'、'谁指向 X'、'X 的出边/入边有哪些'时使用。"),
        };
    }

    // ══════════════════════════════════════════════════════
    // 私有：系统提示
    // ══════════════════════════════════════════════════════

    private static string BuildInstructions() => """
你是一个专业的图数据库助手。你可以调用以下工具函数查询真实数据：
- GetNeighbors：查询节点的邻居（出边+入边）
- GetRelations：查询两节点之间的直接关系
- ListAllNodes：列出所有节点和关系
- GetGraphSize：获取图规模（节点数+边数）
- FindPath：查找两节点之间的最短路径
- SearchNodes：按关键词搜索节点名（部分匹配）
- GetSubgraph：从起始节点出发 N 跳内的子图（1-3 跳）
- GetEdgesOfNode：查询节点的出边和入边（区分方向）

工作规则：
1. 回答任何与图数据相关的问题前，必须先调用合适的工具函数获取真实数据。
2. 调用工具后，必须用自然语言总结结果回答用户。不要输出工具的原始调用格式。
3. 不要编造数据。如果工具返回"未找到"，如实告知用户。
4. 调用工具时，graphName 参数使用用户消息中提到的图名；如果用户没提，使用会话上下文。
5. 选择最合适的工具：
   - 用户给出了完整节点名 → 用 GetNeighbors / GetRelations / GetEdgesOfNode
   - 用户给出关键词但不确定节点名 → 用 SearchNodes
   - 用户想探索周边结构 → 用 GetSubgraph
6. 回答要简洁、准确、使用中文。
""";
}
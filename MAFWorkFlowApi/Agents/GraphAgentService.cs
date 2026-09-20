using MAFWorkFlowApi.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// AgentSessionStore 已迁移至 Abstractions 包
using AgentSessionStore = Microsoft.Agents.AI.Hosting.AgentSessionStore;

namespace MAFWorkFlowApi.Agents;

#pragma warning disable MAAI001

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

    public async Task<AgentReply> ChatAsync(
        string? conversationId,
        string userMessage,
        CancellationToken ct)
    {
        var currentConversationId = conversationId ?? Guid.NewGuid().ToString("N");

        _toolCtx.Reset();

        var session = await _sessionStore.GetSessionAsync(
                          _graphAgent, currentConversationId, ct)
                      ?? await _graphAgent.CreateSessionAsync(cancellationToken: ct);

        var response = await _graphAgent.RunAsync(
            userMessage, session, cancellationToken: ct);

        await _sessionStore.SaveSessionAsync(
            _graphAgent, currentConversationId, session, ct);

        // ★ 读取工具调用详情列表
        var toolsInvoked = _toolCtx.Records.Select(r => r.ToolName).ToList();
        var toolCallDetails = _toolCtx.Records
            .Select(r => new ToolCallDetailDto(
                ToolName: r.ToolName,
                Arguments: r.Arguments,
                Result: TruncateForUi(r.Result, 800),
                ElapsedMs: r.ElapsedMs,
                Success: r.Success,
                FromCache: r.FromCache))   // ★ 新增
            .ToList();

        var finalReply = SanitizeReply(response, toolsInvoked);

        return new AgentReply(
            ConversationId: currentConversationId,
            AgentName: _graphAgent.Name ?? "GraphAssistant",
            Reply: finalReply,
            MessageCount: response.Messages.Count,
            ToolsInvoked: toolsInvoked,
            ToolCallDetails: toolCallDetails);
    }

    private static string? TruncateForUi(string? text, int maxLen)
        => string.IsNullOrEmpty(text)
            ? text
            : (text.Length > maxLen ? text[..maxLen] + "…" : text);

    public ValueTask<IReadOnlyList<string>> ListConversationsAsync(CancellationToken ct)
        => _catalog.ListConversationIdsAsync(ct);

    public ValueTask<bool> ResetConversationAsync(string conversationId, CancellationToken ct)
        => _catalog.DeleteConversationAsync(conversationId, ct);

    public ValueTask<IReadOnlyList<SessionMessage>> GetSessionMessagesAsync(
        string conversationId, CancellationToken ct)
        => _catalog.GetSessionMessagesAsync(conversationId, ct);

    // ══════════════════════════════════════════════════════

    private string SanitizeReply(AgentResponse response, List<string> toolsInvoked)
    {
        var rawText = response.Text;

        if (!string.IsNullOrEmpty(rawText) && !ReplySanitizer.ContainsToolCallMarkers(rawText))
            return rawText.Trim();

        _logger.LogWarning("检测到 tool_call 残留标记或无文本，尝试从消息历史提取最终回答");

        var fallback = response.Messages
            .Where(m => m.Role == ChatRole.Assistant)
            .Select(m => m.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t) && !ReplySanitizer.ContainsToolCallMarkers(t))
            .LastOrDefault();

        if (!string.IsNullOrWhiteSpace(fallback)) return fallback.Trim();

        if (toolsInvoked.Count > 0)
            return $"抱歉，我在调用工具 {string.Join("、", toolsInvoked)} 后没能生成最终回答。" +
                   "请换一种问法，或者稍后重试。";

        return "抱歉，我无法完成这个请求。请换个方式提问，或者确认图名是否正确。";
    }

    // ══════════════════════════════════════════════════════

    private static IList<AITool> BuildTools(GraphTools t)
    {
        return new List<AITool>
        {
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

            AIFunctionFactory.Create(
                (Func<string, string, CancellationToken, Task<string>>)
                ((keyword, graphName, ct) => t.SearchNodesAsync(keyword, graphName, ct)),
                name: "SearchNodes",
                description: "按关键词搜索名称包含该关键词的节点（不区分大小写、部分匹配）。" +
                             "【强制使用场景】当用户问'和 XX 相关的节点'、'有哪些 XX'、" +
                             "'有没有 XX'、'XX 类型的东西'时，必须先调用此工具确认图中实际节点，" +
                             "严禁凭记忆列出常见概念（如 MySQL、Redis 等图中可能不存在的节点）。"),

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

                                                 【绝对规则】
                                                 1. 你必须先调用工具函数获取真实数据，才能回答任何与图数据相关的问题。
                                                 2. 严禁凭常识或记忆编造节点名、关系名。图中的节点名是权威数据。
                                                 3. 即使用户问的是"常见的概念"（如"数据库"、"机器学习"），也必须先调用 SearchNodes 或 ListAllNodes 确认图中实际存在哪些节点。
                                                 4. 工具返回"未找到"时，如实告知用户"图中没有该节点"，不要补充想象中的节点。
                                                 5. 调用工具后，用自然语言总结结果回答用户，不要输出工具的原始调用格式。

                                                 【工具选择指南】
                                                 - 用户给了完整节点名 → GetNeighbors / GetRelations / GetEdgesOfNode
                                                 - 用户给了关键词但不确定节点名 → SearchNodes
                                                 - 用户想探索周边 → GetSubgraph
                                                 - 用户问"有哪些/全部" → ListAllNodes 或 SearchNodes
                                                 """;
}
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using TreeGraph.MafWfApi.Models;
using TreeGraph.Shared.NodeEavSky.Dtos;
using AgentSessionStore = Microsoft.Agents.AI.AgentSessionStore;

namespace TreeGraph.MafWfApi.Agents;

/// <summary>
/// 用户指点单节点 → 加载子树 + EAV → LLM 合成回答。
/// 作为一次正常对话轮次呈现，写入会话历史。
/// </summary>
public sealed class NodeExpandService
{
    private const int DefaultMaxDepth = 3;
    private const int DefaultMaxNodes = 50;

    private readonly SubtreeLoader _subtreeLoader;
    private readonly AgentSessionStore _sessionStore;
    private readonly AIAgent _expandAgent;
    private readonly ILogger<NodeExpandService> _logger;

    public NodeExpandService(
        SubtreeLoader subtreeLoader,
        IChatClient chatClient,
        AgentSessionStore sessionStore,
        ILoggerFactory loggerFactory)
    {
        _subtreeLoader = subtreeLoader;
        _sessionStore = sessionStore;
        _logger = loggerFactory.CreateLogger<NodeExpandService>();

        _expandAgent = chatClient.AsAIAgent(
            instructions: BuildInstructions(),
            name: "NodeExpander");
    }

    public async IAsyncEnumerable<StreamChunk> ExpandAsConversationAsync(
        string? conversationId,
        string inodeId,
        string hostType,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var currentConversationId = conversationId ?? Guid.NewGuid().ToString("N");

        // 1. 加载子树
        var subtree = await _subtreeLoader.LoadAsync(
            hostType, inodeId, DefaultMaxDepth, DefaultMaxNodes, ct);

        if (subtree is null)
        {
            yield return new StreamChunk
            {
                Type = StreamChunkType.Start,
                ConversationId = currentConversationId,
                AgentName = "NodeExpander"
            };
            yield return new StreamChunk
            {
                Type = StreamChunkType.Done,
                Text = $"未找到节点 {inodeId}",
                ConversationId = currentConversationId,
                AgentName = "NodeExpander"
            };
            yield break;
        }

        // 2. Start
        yield return new StreamChunk
        {
            Type = StreamChunkType.Start,
            ConversationId = currentConversationId,
            AgentName = "NodeExpander"
        };

        // 3. 复用现有会话（后续对话延续上下文）
        var session = await _sessionStore.GetSessionAsync(
                          _expandAgent,
                          new AgentSessionStoreKey(currentConversationId), ct)
                      ?? await _expandAgent.CreateSessionAsync(cancellationToken: ct);

        // 4. 构造 LLM 输入，走 agent 流式管道（会话会被自动持久化）
        var userPrompt = $"""
            请综合描述以下节点及其子树的内容：
            {FormatSubtreeAsText(subtree)}
            """;

        var accumulated = new StringBuilder();

        await foreach (var update in _expandAgent
                           .RunStreamingAsync(userPrompt, session, cancellationToken: ct))
        {
            var delta = update.Text;
            if (string.IsNullOrEmpty(delta)) continue;

            accumulated.Append(delta);

            yield return new StreamChunk
            {
                Type = StreamChunkType.Delta,
                Text = delta
            };
        }

        // 5. 写入会话历史
        await _sessionStore.SaveSessionAsync(
            _expandAgent,
            new AgentSessionStoreKey(currentConversationId),
            session, ct);

        // 6. Done
        var finalText = accumulated.Length > 0
            ? accumulated.ToString()
            : "（模型未返回内容）";

        yield return new StreamChunk
        {
            Type = StreamChunkType.Done,
            Text = finalText,
            ConversationId = currentConversationId,
            AgentName = "NodeExpander",
            ToolsInvoked = new List<string> { "ExpandNode" }
        };
    }

    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 非流式版本：加载子树 → LLM 一次性合成 → 返回全文。
    /// 供独立端点 POST /api/graph-agent/expand-node 使用。
    /// </summary>
    public async Task<NodeExpandResult> ExpandAsync(
        string? conversationId,
        string inodeId,
        string hostType,
        int depth,
        CancellationToken ct)
    {
        var currentConversationId = conversationId ?? Guid.NewGuid().ToString("N");

        // 子树层数由调用方（UI 参数）控制：0 = 仅节点本身，默认 3 层
        var maxDepth = Math.Clamp(depth, 0, 10);
        var subtree = await _subtreeLoader.LoadAsync(
            hostType, inodeId, maxDepth, DefaultMaxNodes, ct);

        if (subtree is null)
        {
            return new NodeExpandResult(
                ConversationId: currentConversationId,
                AgentName: "NodeExpander",
                Reply: $"未找到节点 {inodeId}",
                Found: false);
        }

        var session = await _sessionStore.GetSessionAsync(
                          _expandAgent,
                          new AgentSessionStoreKey(currentConversationId), ct)
                      ?? await _expandAgent.CreateSessionAsync(cancellationToken: ct);

        var scopeText = maxDepth > 0 ? $"节点及其 {maxDepth} 层子树" : "节点本身（不含子项）";
        var userPrompt = $"""
            请综合描述以下{scopeText}的内容：
            {FormatSubtreeAsText(subtree)}
            """;

        var response = await _expandAgent.RunAsync(
            userPrompt, session, cancellationToken: ct);

        await _sessionStore.SaveSessionAsync(
            _expandAgent,
            new AgentSessionStoreKey(currentConversationId),
            session, ct);

        var text = response.Text;
        return new NodeExpandResult(
            ConversationId: currentConversationId,
            AgentName: "NodeExpander",
            Reply: string.IsNullOrWhiteSpace(text) ? "（模型未返回内容）" : text,
            Found: true);
    }

    private static string FormatSubtreeAsText(SubtreeNodeDto node)
    {
        var sb = new StringBuilder();
        AppendNode(sb, node, depth: 0);
        return sb.ToString();
    }

    private static void AppendNode(StringBuilder sb, SubtreeNodeDto node, int depth)
    {
        var indent = new string(' ', depth * 2);

        sb.Append(indent).Append("● ").AppendLine(node.Name);

        if (depth == 0 && !string.IsNullOrEmpty(node.Path))
            sb.Append(indent).Append("  路径：").AppendLine(node.Path);

        if (!string.IsNullOrEmpty(node.EavRendered))
            sb.AppendLine(node.EavRendered);

        if (node.Children is { Count: > 0 })
        {
            sb.Append(indent).AppendLine("  子节点：");
            foreach (var child in node.Children)
                AppendNode(sb, child, depth + 1);
        }
    }

    private static string BuildInstructions() => """
        你是数据分析助手。用户点击了一个节点，已收集该节点（可能含若干层子树）的全部信息，需要你综合分析后以小结的形式回答。

        【核心要求】
        - 不要描述节点的结构：路径、层级、目录树、位置关系等一律不写。
        - 把收集到的节点信息（实体属性、记录内容等）综合起来分析，提炼出这些信息共同说明了什么。
        - 输出是一段连贯的小结（结论性文字），不是逐条罗列原始数据，也不是复述结构。

        【规则】
        1. 用自然语言回答，不要输出 JSON、代码块或原始数据结构。
        2. 只陈述数据中的事实，具体数字优先（"3 条记录"，不是"多条记录"）。
        3. 涉及时间时给出具体日期。
        4. 严禁编造数据里没有的内容。
        5. 严禁任何假设、可能、推测、估计、设想、猜测类虚拟内容；"可能""或许""假设""推测"等措辞一律不得出现。
        6. 某方面没有数据就不呈现该部分，必要时直接说明"没有相关内容"。
        7. 若没有任何实质数据，直接说明"该节点暂无内容"。
        8. 长度 200-500 字，简洁清晰。

        【回答风格】
        - 直接给出小结，不要寒暄。
        """;
}

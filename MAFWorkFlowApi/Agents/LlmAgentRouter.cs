using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 用 LLM 做意图路由，但优先走规则拦截：
/// 1. 现实问题词库 → 直接 assistant（0ms）
/// 2. 元问题关键词 → 直接 assistant（0ms）
/// 3. 缓存命中 → 直接返回
/// 4. 以上都不命中 → 才调用 LLM 兜底
/// </summary>
public sealed class LlmAgentRouter
{
    private const string RouteGraph = "graph";
    private const string RouteAssistant = "assistant";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    // ★ 现实问题词库：命中任一直接判 assistant，不调用 LLM
    private static readonly string[] ObviousNonGraphKeywords =
    {
        // 天气
        "天气", "气温", "下雨", "晴天", "阴天", "预报", "温度",
        // 时间/日期
        "几点", "现在时间", "日期", "今天是几", "星期几",
        // 新闻/时事
        "新闻", "热点", "时事", "最近发生", "今日头条",
        // 计算
        "算一下", "计算一下", "帮我算",
        // 常识问答
        "介绍一下", "什么是", "历史", "地理",
        // 生活
        "菜谱", "怎么做菜", "股票", "汇率", "健康",
    };

    private readonly IChatClient _chatClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LlmAgentRouter> _logger;

    public LlmAgentRouter(
        [FromKeyedServices("chat-model")] IChatClient chatClient,
        IMemoryCache cache,
        ILogger<LlmAgentRouter> logger)
    {
        _chatClient = chatClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> RouteAsync(string userMessage, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return RouteGraph;

        var trimmed = userMessage.Trim();

        // ★ 1. 规则拦截：现实问题词库
        if (IsObviousNonGraph(trimmed))
        {
            _logger.LogInformation(
                "[LLMRouter] 规则拦截（现实问题）：'{Msg}' → assistant", trimmed);
            return RouteAssistant;
        }

        // ★ 2. 规则拦截：元问题关键词
        if (AgentRouter.ShouldRouteToAssistant(trimmed))
        {
            _logger.LogInformation(
                "[LLMRouter] 规则拦截（元问题）：'{Msg}' → assistant", trimmed);
            return RouteAssistant;
        }

        // ★ 3. 缓存
        var cacheKey = $"router|{trimmed}";
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
        {
            _logger.LogDebug("[LLMRouter] 缓存命中：'{Msg}' → {Route}", trimmed, cached);
            return cached;
        }

        // ★ 4. LLM 兜底
        var route = RouteGraph;
        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, RouterPrompt),
                new(ChatRole.User, trimmed)
            };

            var options = new ChatOptions
            {
                MaxOutputTokens = 8,
                Temperature = 0f
            };

            var response = await _chatClient.GetResponseAsync(messages, options, ct);
            var text = (response.Text ?? "").Trim().ToLowerInvariant();

            if (text.Contains("assistant", StringComparison.OrdinalIgnoreCase))
                route = RouteAssistant;
            else if (text.Contains("graph", StringComparison.OrdinalIgnoreCase))
                route = RouteGraph;
            else
                _logger.LogWarning("[LLMRouter] LLM 返回无法解析：'{Text}'，回退到 graph", text);

            _logger.LogInformation("[LLMRouter] LLM 路由：'{Msg}' → {Route}", trimmed, route);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LLMRouter] LLM 调用失败，回退到关键词规则");
            route = AgentRouter.ShouldRouteToAssistant(trimmed) ? RouteAssistant : RouteGraph;
        }

        // ★ 5. 写入缓存
        _cache.Set(cacheKey, route, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl,
            Size = 1
        });

        return route;
    }

    private static bool IsObviousNonGraph(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        foreach (var kw in ObviousNonGraphKeywords)
        {
            if (message.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    // ══════════════════════════════════════════════════════
    // LLM Router Prompt（兜底用，处理真正的边界问题）
    // ══════════════════════════════════════════════════════
    private const string RouterPrompt = """
                                        你是一个意图分类器。把用户消息分为 graph 或 assistant 两类。

                                        【graph】涉及具体节点名、图名、或图结构
                                        例："TestGraph 有哪些节点"、"根节点C 的邻居"、"AI 到 Python 怎么走"

                                        【assistant】与图数据无关
                                        例："今天天气"、"现在几点"、"你好"、"你能做什么"、"谢谢"

                                        规则：
                                        - 只要消息包含具体节点名/图名/图结构词 → graph
                                        - 其他所有情况 → assistant（因为误判为 graph 会让用户等 30+ 秒）

                                        只输出一个词：graph 或 assistant
                                        """;
}
namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 极简路由：根据用户消息关键词判断交给哪个 Agent。
/// 主场景是图数据查询，所以默认走 GraphAgent。
/// 只有明确的元问题（问候、能力询问）才走 AssistantAgent。
/// </summary>
public static class AgentRouter
{
    private static readonly string[] MetaKeywords =
    {
        // 问候
        "你好", "您好", "hi", "hello", "hey",
        // 能力询问
        "你是谁", "你能做什么", "能做什么", "能帮我做什么",
        "你能干什么", "会做什么", "有什么功能", "有什么能力",
        // 使用帮助
        "怎么用", "如何使用", "帮助", "help", "用法", "使用说明",
        // 社交
        "谢谢", "感谢", "thanks", "thank you",
        // 关于系统
        "这个系统", "这个工具", "这个应用", "这个助手",
    };

    /// <summary>返回 true 表示应路由到 AssistantAgent。</summary>
    public static bool ShouldRouteToAssistant(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        var lower = message.Trim().ToLowerInvariant();
        foreach (var kw in MetaKeywords)
        {
            if (lower.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
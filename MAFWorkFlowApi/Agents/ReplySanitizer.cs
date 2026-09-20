namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 统一的输出清理器：检测 LLM 偶尔泄漏的 tool_call 内部标记。
/// 被 GraphAgentService（实时回复）和 RedisAgentSessionStore（历史回放）共用。
/// </summary>
public static class ReplySanitizer
{
    /// <summary>检测文本是否含 LLM tool-call 内部标记。</summary>
    public static bool ContainsToolCallMarkers(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        return text.Contains("<tool_call>", StringComparison.OrdinalIgnoreCase)
               || text.Contains("</tool_call>", StringComparison.OrdinalIgnoreCase)
               || text.Contains("<|tool_call|>", StringComparison.OrdinalIgnoreCase)
               || text.Contains("ronics", StringComparison.OrdinalIgnoreCase)
               || text.Contains("\"arguments\":", StringComparison.OrdinalIgnoreCase)
               || text.Contains("\"name\":\"Get", StringComparison.OrdinalIgnoreCase)
               || text.Contains("\"name\": \"Get", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 清理文本：含标记时返回空字符串。
    /// 用于历史回放跳过无效的 assistant 消息。
    /// </summary>
    public static string CleanOrEmpty(string? text)
        => ContainsToolCallMarkers(text) ? string.Empty : (text ?? string.Empty).Trim();
}
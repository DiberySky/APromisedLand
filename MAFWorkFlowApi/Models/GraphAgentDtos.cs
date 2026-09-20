using System.ComponentModel.DataAnnotations;

namespace MAFWorkFlowApi.Models;

/// <summary>POST /api/graph-agent/chat 请求体。</summary>
public sealed class GraphAgentRequest
{
    /// <summary>会话 ID。首次为空，后续带上服务端返回的 ID。</summary>
    public string? ConversationId { get; set; }

    /// <summary>用户问题。建议在图名后加空格，例如："TestGraph 根节点C 有哪些邻居？"</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(4000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}

/// <summary>POST /api/graph-agent/chat 响应体。</summary>
public sealed record GraphAgentReply(
    string ConversationId,
    string AgentName,
    string Reply,
    int MessageCount,
    IReadOnlyList<string> ToolsInvoked);
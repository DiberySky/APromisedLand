using System.ComponentModel.DataAnnotations;

namespace MAFWorkFlowApi.Models;

/// <summary>POST /api/graph-agent/chat 请求体。</summary>
public sealed class GraphAgentRequest
{
    public string? ConversationId { get; set; }

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
    IReadOnlyList<string> ToolsInvoked,
    IReadOnlyList<ToolCallDetailDto> ToolCallDetails);

// ★ 新增：会话列表带显示名
public sealed record SessionSummary(string Id, string? DisplayName);

public sealed record GraphAgentSessionsReply(IReadOnlyList<SessionSummary> Sessions);

/// <summary>POST /api/graph-agent/sessions/{id}/rename 请求体。</summary>
public sealed class RenameSessionRequest
{
    [StringLength(200)]
    public string? DisplayName { get; set; }
}
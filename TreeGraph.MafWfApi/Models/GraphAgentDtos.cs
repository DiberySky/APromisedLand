using System.ComponentModel.DataAnnotations;

namespace TreeGraph.MafWfApi.Models;

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

/// <summary>POST /api/graph-agent/expand-node 请求体。</summary>
public sealed class ExpandNodeRequest
{
    public string? ConversationId { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string InodeId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string HostType { get; set; } = string.Empty;

    /// <summary>是否包含子项（false = 仅节点自身，maxDepth=1）。</summary>
    public bool IncludeChildren { get; set; } = true;
}

/// <summary>POST /api/graph-agent/expand-node 响应体（非流式一次性返回）。</summary>
public sealed record NodeExpandResult(
    string ConversationId,
    string AgentName,
    string Reply,
    bool Found);
using System.ComponentModel.DataAnnotations;

namespace TreeGraph.Shared.MafWfSky.Contracts;

// ══════════════════════════════════════════════════════
// Graph Agent（/api/graph-agent/*）与通用助手（/api/Agents/*）共用形状
// ══════════════════════════════════════════════════════

/// <summary>POST /api/graph-agent/chat、/api/Agents/chat 请求体。</summary>
public sealed class AgentChatRequest
{
    public string? ConversationId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(4000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}

/// <summary>工具调用详情（前端展示用）。</summary>
public sealed record ToolCallDetailDto(
    string ToolName,
    string Arguments,
    string? Result,
    long ElapsedMs,
    bool Success,
    bool FromCache);

/// <summary>POST /api/graph-agent/chat、/api/Agents/chat 响应体。</summary>
public sealed record AgentReplyDto(
    string ConversationId,
    string AgentName,
    string Reply,
    int MessageCount,
    IReadOnlyList<string>? ToolsInvoked = null,
    IReadOnlyList<ToolCallDetailDto>? ToolCallDetails = null);

/// <summary>会话摘要（含显示名）。</summary>
public sealed record SessionSummaryDto(string Id, string? DisplayName);

/// <summary>GET /api/graph-agent/sessions 响应体。</summary>
public sealed record GraphAgentSessionsReplyDto(IReadOnlyList<SessionSummaryDto> Sessions);

/// <summary>GET /api/Agents/sessions 响应体。</summary>
public sealed record SessionsReplyDto(IReadOnlyList<string> Sessions);

/// <summary>单条历史消息。</summary>
public sealed record SessionMessageDto(string Role, string Text, string? AuthorName);

/// <summary>GET .../sessions/{id}/messages 响应体。</summary>
public sealed record SessionMessagesReplyDto(
    string ConversationId,
    IReadOnlyList<SessionMessageDto> Messages);

/// <summary>POST /api/graph-agent/sessions/{id}/rename 请求体。</summary>
public sealed class RenameSessionRequest
{
    [StringLength(200)]
    public string? DisplayName { get; set; }
}

// ══════════════════════════════════════════════════════
// 流式（/api/graph-agent/chat/stream，SSE）
// ══════════════════════════════════════════════════════

public enum StreamChunkType { Start, Delta, Done }

/// <summary>Graph Agent 流式块（SSE data 载荷）。</summary>
public sealed class StreamChunkDto
{
    public StreamChunkType Type { get; set; }
    public string? Text { get; set; }
    public string? ConversationId { get; set; }
    public IReadOnlyList<string>? ToolsInvoked { get; set; }
    public IReadOnlyList<ToolCallDetailDto>? ToolCallDetails { get; set; }
    public string? AgentName { get; set; }
}

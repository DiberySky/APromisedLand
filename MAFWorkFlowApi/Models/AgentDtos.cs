using System.ComponentModel.DataAnnotations;

namespace MAFWorkFlowApi.Models;

public sealed class ChatRequest
{
    public string? ConversationId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(4000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}

/// <summary>工具调用详情（用于前端展示）。</summary>
public sealed record ToolCallDetailDto(
    string ToolName,
    string Arguments,
    string? Result,
    long ElapsedMs,
    bool Success);

/// <summary>POST /api/agents/chat 的响应体。</summary>
public sealed record AgentReply(
    string ConversationId,
    string AgentName,
    string Reply,
    int MessageCount,
    IReadOnlyList<string>? ToolsInvoked = null,
    IReadOnlyList<ToolCallDetailDto>? ToolCallDetails = null);

public sealed record SessionsReply(IReadOnlyList<string> Sessions);

public sealed record SessionMessage(
    string Role,
    string Text,
    string? AuthorName);

public sealed record SessionMessagesReply(
    string ConversationId,
    IReadOnlyList<SessionMessage> Messages);

public sealed class WorkflowRunRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(2000, MinimumLength = 1)]
    public string Topic { get; set; } = string.Empty;
}

public sealed record WorkflowStep(string Agent, string Text);

public sealed record WorkflowReply(
    string Topic,
    string FinalAnswer,
    IReadOnlyList<WorkflowStep> Steps);

public sealed record WorkflowStreamEvent(
    string Agent,
    string Delta,
    bool IsFinal);
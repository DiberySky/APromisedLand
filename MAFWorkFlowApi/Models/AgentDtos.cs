using System.ComponentModel.DataAnnotations;

namespace MAFWorkFlowApi.Models;

/// <summary>POST /api/agents/chat 的请求体。</summary>
public sealed class ChatRequest
{
    /// <summary>
    /// 会话 ID。首次请求时为空，后续请求需带上服务端返回的 conversationId。
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>用户输入。必填。</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(4000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}

/// <summary>POST /api/agents/chat 的响应体。</summary>
public sealed record AgentReply(
    string ConversationId,
    string AgentName,
    string Reply,
    int MessageCount);

/// <summary>POST /api/workflows/writer-critic 的请求体。</summary>
public sealed class WorkflowRunRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(2000, MinimumLength = 1)]
    public string Topic { get; set; } = string.Empty;
}

/// <summary>工作流中单个 Agent 的输出。</summary>
public sealed record WorkflowStep(string Agent, string Text);

/// <summary>POST /api/workflows/writer-critic 的响应体。</summary>
public sealed record WorkflowReply(
    string Topic,
    string FinalAnswer,
    IReadOnlyList<WorkflowStep> Steps);
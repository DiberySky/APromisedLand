using System.ComponentModel.DataAnnotations;

namespace TreeGraph.Shared.MafWfSky.Contracts;

/// <summary>POST /api/Workflows/writer-critic(+stream) 请求体。</summary>
public sealed class WorkflowRunRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(2000, MinimumLength = 1)]
    public string Topic { get; set; } = string.Empty;
}

public sealed record WorkflowStepDto(string Agent, string Text);

/// <summary>POST /api/Workflows/writer-critic 响应体。</summary>
public sealed record WorkflowReplyDto(
    string Topic,
    string FinalAnswer,
    IReadOnlyList<WorkflowStepDto> Steps);

/// <summary>工作流 SSE 事件（data 载荷）。</summary>
public sealed record WorkflowStreamEventDto(string Agent, string Delta, bool IsFinal);

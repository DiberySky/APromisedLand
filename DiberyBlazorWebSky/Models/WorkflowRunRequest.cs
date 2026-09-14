namespace DiberyBlazorWebSky.Models;

/// <summary>与 API 的 WorkflowRunRequest 对应。</summary>
public record WorkflowRunRequest
{
    /// <summary>工作流主题。必填。</summary>
    public required string Topic { get; init; }
}
namespace DiberyBlazorWebSky.Models;

/// <summary>与 API 的 WorkflowReply 对应。</summary>
public record WorkflowReply
{
    /// <summary>原始主题。</summary>
    public required string Topic { get; init; }

    /// <summary>工作流的最终答案。</summary>
    public required string FinalAnswer { get; init; }

    /// <summary>工作流中每个 Agent 的输出步骤。</summary>
    public required List<WorkflowStep> Steps { get; init; }
}
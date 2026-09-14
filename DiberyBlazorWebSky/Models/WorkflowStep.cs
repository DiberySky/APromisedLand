namespace DiberyBlazorWebSky.Models;

/// <summary>与 API 的 WorkflowStep 对应。</summary>
public record WorkflowStep
{
    /// <summary>产生该步骤输出的 Agent 名称。</summary>
    public required string Agent { get; init; }

    /// <summary>该步骤的文本输出。</summary>
    public required string Text { get; init; }
}
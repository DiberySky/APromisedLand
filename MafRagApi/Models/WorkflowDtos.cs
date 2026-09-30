namespace MafRagApi.Models;

/// <summary>
/// MAF 工作流运行请求。
/// </summary>
public sealed class WorkflowRunRequestDto
{
    /// <summary>工作流输入主题/问题。</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>可选：工作流名称（默认 writer-critic）。</summary>
    public string? Workflow { get; set; }
}

/// <summary>
/// 工作流单个步骤的输出。
/// </summary>
public sealed class WorkflowStepDto
{
    /// <summary>产出该步骤的 Agent 名称。</summary>
    public string Agent { get; set; } = string.Empty;

    /// <summary>该步骤的文本输出。</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// 工作流非流式响应。
/// </summary>
public sealed class WorkflowRunResponseDto
{
    /// <summary>原始输入主题。</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>工作流最终答案。</summary>
    public string FinalAnswer { get; set; } = string.Empty;

    /// <summary>各 Agent 步骤产出。</summary>
    public IReadOnlyList<WorkflowStepDto> Steps { get; set; } = Array.Empty<WorkflowStepDto>();
}

/// <summary>
/// 工作流流式事件。
/// </summary>
public sealed class WorkflowStreamEventDto
{
    /// <summary>产出该增量的 Agent 名称。</summary>
    public string Agent { get; set; } = string.Empty;

    /// <summary>增量文本。</summary>
    public string Delta { get; set; } = string.Empty;

    /// <summary>是否最终帧。</summary>
    public bool IsFinal { get; set; }
}

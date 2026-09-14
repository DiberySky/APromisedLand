namespace DiberyBlazorWebSky.Models;

/// <summary>与 API 的 WorkflowStreamEvent 对应（SSE 单帧）。</summary>
public record WorkflowStreamEvent
{
    public required string Agent { get; init; }
    public required string Delta { get; init; }
    public bool IsFinal { get; init; }
}
// Models/LoopStreamChunkDto.cs
namespace MafRagApi.Models;

public sealed class LoopStreamChunkDto
{
    public string SessionId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    public int Round { get; set; }
    public int MaxRounds { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>start | round_start | delta | tool_call | tool_result | round_end | done | error</summary>
    public string Phase { get; set; } = "delta";

    public bool Done { get; set; }

    /// <summary>仅 Phase=done 的收尾帧有意义。</summary>
    public bool Truncated { get; set; }

    /// <summary>仅 Phase=tool_call 时有值。</summary>
    public string? ToolName { get; set; }
    public string? ToolCallId { get; set; }
    public string? ToolArgumentsJson { get; set; }

    /// <summary>仅 Phase=tool_result 时有值。</summary>
    public string? ToolResultText { get; set; }
}
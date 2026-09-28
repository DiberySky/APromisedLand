// Models/LoopStreamChunkDto.cs
namespace MafSampleApi.Models;

public sealed class LoopStreamChunkDto
{
    public string SessionId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    public int Round { get; set; }
    public int MaxRounds { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>start | round_start | delta | round_end | done | error</summary>
    public string Phase { get; set; } = "delta";

    public bool Done { get; set; }

    /// <summary>仅 Phase=done 的收尾帧有意义。</summary>
    public bool Truncated { get; set; }          // ★★★ 就是这一行
}
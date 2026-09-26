// Models/LoopChatResponseDto.cs
namespace MafSampleApi.Models;

public sealed class LoopChatResponseDto
{
    public string SessionId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int MaxRounds { get; set; }

    /// <summary>实际完成的轮数（可能因时间预算被截断）。</summary>
    public int CompletedRounds { get; set; }

    /// <summary>true 表示时间预算耗尽，未跑满 MaxRounds。</summary>
    public bool Truncated { get; set; }

    /// <summary>每一轮的输出，按轮次顺序排列。</summary>
    public List<LoopRoundDto> Rounds { get; set; } = new();

    /// <summary>最后一轮的文本，便捷字段。</summary>
    public string Content { get; set; } = string.Empty;
}

public sealed class LoopRoundDto
{
    public int Round { get; set; }
    public string Content { get; set; } = string.Empty;
}
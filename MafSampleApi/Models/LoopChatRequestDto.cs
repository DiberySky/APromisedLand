// Models/LoopChatRequestDto.cs
namespace MafSampleApi.Models;

public sealed class LoopChatRequestDto
{
    public string? SessionId { get; set; }
    public string? Model { get; set; }

    /// <summary>首轮用户输入。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 下一轮的提示词模板。可用 {previous} 占位上一轮的完整回答。
    /// 为空时默认使用「继续」让 Agent 自行延续。
    /// </summary>
    public string? ContinuePrompt { get; set; }

    /// <summary>最大循环轮数，默认 3，服务端会夹紧到 [1, 20]。</summary>
    public int MaxRounds { get; set; } = 3;
}
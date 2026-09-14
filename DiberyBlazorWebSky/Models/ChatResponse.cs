namespace DiberyBlazorWebSky.Models;

/// <summary>与 API 的 AgentReply 对应。</summary>
public record ChatResponse
{
    /// <summary>本次会话 ID。</summary>
    public required string ConversationId { get; init; }

    /// <summary>产生本次响应的 Agent 名称。</summary>
    public string? AgentName { get; init; }

    /// <summary>助手回复文本。</summary>
    public required string Reply { get; init; }

    /// <summary>本次运行产生的消息数量（不含历史）。</summary>
    public int MessageCount { get; init; }
}
namespace DiberyBlazorWebSky.Models;

/// <summary>与 API 的 ChatRequest 对应。</summary>
public record ChatRequest
{
    /// <summary>缺省时服务端生成新会话 ID。</summary>
    public string? ConversationId { get; init; }

    /// <summary>用户消息。</summary>
    public required string Message { get; init; }
}
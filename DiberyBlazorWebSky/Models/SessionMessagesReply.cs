namespace DiberyBlazorWebSky.Models;

/// <summary>与 API 的 SessionMessagesReply 对应。</summary>
public record SessionMessagesReply
{
    public required string ConversationId { get; init; }
    public required List<SessionMessage> Messages { get; init; }
}
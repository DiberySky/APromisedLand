namespace MafStatefulApi.Models;

/// <summary>
/// Response model for the chat endpoint.
/// </summary>
public record ChatResponse
{
    /// <summary>
    /// The conversation ID for this session.
    /// </summary>
    public required string ConversationId { get; init; }

    /// <summary>
    /// The agent's response to the user's message.
    /// </summary>
    public required string Answer { get; init; }
}

namespace MafAIService.Models;

/// <summary>
/// Request model for the chat endpoint.
/// </summary>
public record ChatRequest
{
    /// <summary>
    /// Optional conversation ID. If not provided, a new conversation will be created.
    /// </summary>
    public string? ConversationId { get; init; }

    /// <summary>
    /// The user's message to the agent.
    /// </summary>
    public required string Message { get; init; }
}

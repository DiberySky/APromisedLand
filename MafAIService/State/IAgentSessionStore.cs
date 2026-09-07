namespace MafAIService.State;

/// <summary>
/// Interface for persisting agent threads (sessions) across requests.
/// </summary>
public interface IAgentSessionStore
{
    /// <summary>
    /// Retrieves a serialized agent thread by conversation ID.
    /// </summary>
    /// <param name="conversationId">The unique conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The serialized thread JSON, or null if not found.</returns>
    Task<string?> GetAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a serialized agent thread.
    /// </summary>
    /// <param name="conversationId">The unique conversation identifier.</param>
    /// <param name="serializedThread">The serialized thread JSON.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync(string conversationId, string serializedThread, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an agent thread by conversation ID.
    /// </summary>
    /// <param name="conversationId">The unique conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all conversation IDs with stored sessions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of conversation IDs.</returns>
    Task<IEnumerable<string>> ListSessionsAsync(CancellationToken cancellationToken = default);
}

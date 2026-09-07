using System.Text.Json;
using MafAIService.State;
using Microsoft.Agents.AI;

namespace MafAIService.Agents;

/// <summary>
/// Orchestrates agent execution with session persistence.
/// Loads chat history state, runs the agent, and saves the updated state.
/// </summary>
public class AgentRunner(
    [FromKeyedServices("AssistantAgent")] AIAgent agent,
    IAgentSessionStore sessionStore,
    ILogger<AgentRunner> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    /// <summary>
    /// Runs the agent with the given message, managing session persistence.
    /// </summary>
    /// <param name="conversationId">The conversation ID to load/save state.</param>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's response.</returns>
    public async Task<string> RunAsync(
        string conversationId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Running agent for conversation {ConversationId}",
            conversationId);

        // Load or create the agent session (stateful)
        var session = await LoadOrCreateSessionAsync(conversationId, cancellationToken);

        // Run the agent with the user message and session
        var response = await agent.RunAsync(userMessage, session, cancellationToken: cancellationToken);
        var answer = response.Text ?? string.Empty;

        // Save the updated session
        await SaveSessionAsync(conversationId, session, cancellationToken);

        logger.LogInformation(
            "Agent response for conversation {ConversationId}: {ResponseLength} chars",
            conversationId,
            answer.Length);

        return answer;
    }

    private async Task<AgentSession> LoadOrCreateSessionAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        var serializedSession = await sessionStore.GetAsync(conversationId, cancellationToken);
        
        if (serializedSession is not null)
        {
            logger.LogDebug(
                "Deserializing existing session for conversation {ConversationId}",
                conversationId);
            
            try
            {
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(serializedSession, JsonOptions);
                // 使用正确的 DeserializeSessionAsync 方法
                return await agent.DeserializeSessionAsync(jsonElement, JsonOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to deserialize session for conversation {ConversationId}, creating new session",
                    conversationId);
            }
        }

        logger.LogDebug(
            "Creating new session for conversation {ConversationId}",
            conversationId);
        
        // 修正：使用 CreateSessionAsync 替代 GetNewSessionAsync
        return await agent.CreateSessionAsync(cancellationToken);
    }

    private async Task SaveSessionAsync(
        string conversationId,
        AgentSession session,
        CancellationToken cancellationToken)
    {
        // 修正：使用 SerializeSessionAsync 替代 session.Serialize
        var serializedElement = await agent.SerializeSessionAsync(session, JsonOptions, cancellationToken);
        var serialized = serializedElement.GetRawText();
        
        logger.LogDebug(
            "Saving session for conversation {ConversationId}, serialized size: {SizeBytes} bytes",
            conversationId,
            serialized.Length);
        
        await sessionStore.SetAsync(conversationId, serialized, cancellationToken);
    }
}
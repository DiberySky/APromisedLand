using MafStatefulApi.Agents;
using MafStatefulApi.Models;
using MafStatefulApi.State;
using Microsoft.AspNetCore.Mvc;

namespace MafStatefulApi.Controllers;

/// <summary>
/// Chat API controller for managing agent conversations with session persistence.
/// </summary>
[ApiController]
[Route("[controller]")]
public class ChatController : ControllerBase
{
    private readonly AgentRunner _agentRunner;
    private readonly IAgentSessionStore _sessionStore;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        AgentRunner agentRunner,
        IAgentSessionStore sessionStore,
        ILogger<ChatController> logger)
    {
        _agentRunner = agentRunner;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    /// <summary>
    /// Send a message to the agent and receive a response.
    /// </summary>
    /// <param name="request">Chat request containing message and optional conversation ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's response with conversation ID.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponse>> PostChat(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var conversationId = request.ConversationId ?? $"{DateTime.Now:HH:mm:ss}-{Guid.NewGuid()}";

        _logger.LogInformation(
            "Chat request received for conversation {ConversationId}",
            conversationId);

        try
        {
            var answer = await _agentRunner.RunAsync(
                conversationId,
                request.Message,
                cancellationToken);

            return Ok(new ChatResponse
            {
                ConversationId = conversationId,
                Answer = answer
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing chat for conversation {ConversationId}",
                conversationId);

            return Problem(
                detail: "An error occurred while processing your request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Reset a conversation by deleting its session state.
    /// </summary>
    /// <param name="conversationId">The conversation ID to reset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Confirmation message.</returns>
    [HttpPost("reset/{conversationId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> ResetConversation(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Reset request received for conversation {ConversationId}",
            conversationId);

        await _sessionStore.DeleteAsync(conversationId, cancellationToken);

        return Ok(new { message = $"Conversation {conversationId} has been reset." });
    }

    /// <summary>
    /// List all active conversation sessions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active session IDs.</returns>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> ListSessions(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing all active sessions");

        var sessions = await _sessionStore.ListSessionsAsync(cancellationToken);

        return Ok(new { sessions });
    }
}

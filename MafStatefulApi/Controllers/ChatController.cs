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
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponse>> PostChat(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var conversationId = ResolveConversationId(request.ConversationId);

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
        catch (TimeoutException ex)
        {
            _logger.LogWarning(
                ex,
                "Timeout acquiring lock for conversation {ConversationId}",
                conversationId);

            return Problem(
                detail: "The conversation is busy, please retry.",
                statusCode: StatusCodes.Status409Conflict);
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
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> ListSessions(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing all active sessions");

        var sessions = await _sessionStore.ListSessionsAsync(cancellationToken);

        return Ok(new { sessions });
    }

    /// <summary>
    /// 规范化 conversationId：
    ///  - 未提供时生成无冒号的 URL 安全 ID（yyyyMMddHHmmss-{Guid:N}）
    ///  - 提供了则替换 URL 路径不安全字符，保证 /reset/{conversationId} 能原样往返
    /// </summary>
    private string ResolveConversationId(string? supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied))
        {
            return $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        }

        var sanitized = supplied
            .Replace(':', '-')
            .Replace('/', '-')
            .Replace('\\', '-')
            .Replace('?', '-')
            .Replace('#', '-')
            .Replace(' ', '-');

        if (!string.Equals(sanitized, supplied, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Supplied conversationId '{Original}' contained URL-unsafe characters; sanitized to '{Sanitized}'",
                supplied,
                sanitized);
        }

        return sanitized;
    }
}
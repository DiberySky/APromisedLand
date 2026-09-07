using MafAIService.Agents;
using MafAIService.Models;
using MafAIService.State;
using Microsoft.AspNetCore.Mvc;

namespace MafAIService.Controllers;

[ApiController]
[Route("")] // 保持端点路径与原来一致：/chat, /reset/{id}, /sessions
public class ChatController(AgentRunner agentRunner, 
    IAgentSessionStore sessionStore,
    ILogger<ChatController> logger)
    : ControllerBase
{
    // POST /chat
    [HttpPost("chat")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var conversationId = request.ConversationId ?? $"{DateTime.Now:HH:mm:ss}-{Guid.NewGuid()}";

        logger.LogInformation("Chat request received for conversation {ConversationId}", conversationId);

        try
        {
            var answer = await agentRunner.RunAsync(conversationId, request.Message, cancellationToken);
            return Ok(new ChatResponse { ConversationId = conversationId, Answer = answer });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing chat for conversation {ConversationId}", conversationId);
            return Problem(
                detail: "An error occurred while processing your request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    // POST /reset/{conversationId}
    [HttpPost("reset/{conversationId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reset(string conversationId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Reset request received for conversation {ConversationId}", conversationId);

        await sessionStore.DeleteAsync(conversationId, cancellationToken);

        return Ok(new { message = $"Conversation {conversationId} has been reset." });
    }

    // GET /sessions
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSessions(CancellationToken cancellationToken)
    {
        logger.LogInformation("Listing all active sessions");

        var sessions = await sessionStore.ListSessionsAsync(cancellationToken);

        return Ok(new { sessions });
    }
}
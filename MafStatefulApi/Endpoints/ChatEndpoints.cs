using MafStatefulApi.Agents;
using MafStatefulApi.Models;
using MafStatefulApi.State;

namespace MafStatefulApi.Endpoints;

/// <summary>
/// Maps the chat-related API endpoints.
/// </summary>
public static class ChatEndpoints
{
    /// <summary>
    /// Maps the chat endpoints to the WebApplication.
    /// </summary>
    public static WebApplication MapChatEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("");

        // POST /chat - Send a message and get a response
        group.MapPost("mini/chat", async (
            ChatRequest request,
            AgentRunner agentRunner,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            // Generate a new conversation ID if not provided
            var conversationId = request.ConversationId ?? $"{DateTime.Now:HH:mm:ss}-{Guid.NewGuid()}";
            
            logger.LogInformation(
                "Chat request received for conversation {ConversationId}",
                conversationId);

            try
            {
                var answer = await agentRunner.RunAsync(
                    conversationId,
                    request.Message,
                    cancellationToken);

                return Results.Ok(new ChatResponse
                {
                    ConversationId = conversationId,
                    Answer = answer
                });
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error processing chat for conversation {ConversationId}",
                    conversationId);
                
                return Results.Problem(
                    detail: "An error occurred while processing your request.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("Chat")
        .WithDescription("Send a message to the agent and receive a response");

        // POST /reset/{conversationId} - Reset a conversation
        group.MapPost("mini/reset/{conversationId}", async (
            string conversationId,
            IAgentSessionStore sessionStore,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation(
                "Reset request received for conversation {ConversationId}",
                conversationId);

            await sessionStore.DeleteAsync(conversationId, cancellationToken);

            return Results.Ok(new { message = $"Conversation {conversationId} has been reset." });
        })
        .WithName("ResetConversation")
        .WithDescription("Reset a conversation by deleting its session state");

        // GET /sessions - List all active sessions
        group.MapGet("mini/sessions", async (
            IAgentSessionStore sessionStore,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Listing all active sessions");

            var sessions = await sessionStore.ListSessionsAsync(cancellationToken);

            return Results.Ok(new { sessions });
        })
        .WithName("ListSessions")
        .WithDescription("List all active conversation sessions");

        return app;
    }
}

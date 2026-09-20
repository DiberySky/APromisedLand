using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace MAFWorkFlowApi.Controllers;

/// <summary>
/// Function Calling 对话入口（LLM 自主调用图工具）。
/// 路由前缀：/api/graph-agent
/// </summary>
[ApiController]
[Route("api/graph-agent")]
[Produces("application/json")]
public sealed class GraphAgentController : ControllerBase
{
    private readonly GraphAgentService _agents;
    private readonly ILogger<GraphAgentController> _logger;

    public GraphAgentController(
        GraphAgentService agents,
        ILogger<GraphAgentController> logger)
    {
        _agents = agents;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<GraphAgentReply>> Chat(
        [FromBody] GraphAgentRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "GraphAgent chat, ConvId={ConvId}, MessageLength={Len}",
            request.ConversationId ?? "(new)", request.Message.Length);

        var reply = await _agents.ChatAsync(
            request.ConversationId, request.Message, ct);

        return Ok(new GraphAgentReply(
            ConversationId: reply.ConversationId,
            AgentName: reply.AgentName,
            Reply: reply.Reply,
            MessageCount: reply.MessageCount,
            ToolsInvoked: reply.ToolsInvoked ?? new List<string>(),
            ToolCallDetails: reply.ToolCallDetails ?? new List<ToolCallDetailDto>()));
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<SessionsReply>> ListSessions(CancellationToken ct)
    {
        var ids = await _agents.ListConversationsAsync(ct);
        return Ok(new SessionsReply(ids));
    }

    [HttpGet("sessions/{conversationId}/messages")]
    public async Task<ActionResult<SessionMessagesReply>> GetSessionMessages(
        [FromRoute] string conversationId, CancellationToken ct)
    {
        var messages = await _agents.GetSessionMessagesAsync(conversationId, ct);
        if (messages.Count == 0)
            return NotFound(new ProblemDetails
            {
                Title = $"会话不存在或没有消息：{conversationId}",
                Status = StatusCodes.Status404NotFound
            });

        return Ok(new SessionMessagesReply(conversationId, messages));
    }

    [HttpPost("reset/{conversationId}")]
    public async Task<IActionResult> Reset(
        [FromRoute] string conversationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return BadRequest();

        var deleted = await _agents.ResetConversationAsync(conversationId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
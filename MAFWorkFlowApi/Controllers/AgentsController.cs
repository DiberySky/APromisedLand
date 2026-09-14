using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace MAFWorkFlowApi.Controllers;

/// <summary>
/// 单 Agent 对话入口（支持持久化多轮会话）。
/// 路由前缀：/api/agents
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class AgentsController : ControllerBase
{
    private readonly MafAgentService _agents;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(
        MafAgentService agents,
        ILogger<AgentsController> logger)
    {
        _agents = agents;
        _logger = logger;
    }

    /// <summary>向通用助手发送一条消息。</summary>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(AgentReply), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentReply>> Chat(
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Chat request, ConversationId={ConvId}, MessageLength={Len}",
            request.ConversationId ?? "(new)", request.Message.Length);

        var reply = await _agents.ChatAsync(
            request.ConversationId, request.Message, ct);

        return Ok(reply);
    }

    /// <summary>列举当前存在的全部会话 ID。</summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(SessionsReply), StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionsReply>> ListSessions(CancellationToken ct)
    {
        var ids = await _agents.ListConversationsAsync(ct);
        return Ok(new SessionsReply(ids));
    }

    /// <summary>获取指定会话的完整消息历史。</summary>
    [HttpGet("sessions/{conversationId}/messages")]
    [ProducesResponseType(typeof(SessionMessagesReply), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionMessagesReply>> GetSessionMessages(
        [FromRoute] string conversationId,
        CancellationToken ct)
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

    /// <summary>删除指定会话（重置会话历史）。</summary>
    [HttpPost("reset/{conversationId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reset(
        [FromRoute] string conversationId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return BadRequest(new ProblemDetails
            {
                Title = "会话 ID 不能为空。",
                Status = StatusCodes.Status400BadRequest
            });

        var deleted = await _agents.ResetConversationAsync(conversationId, ct);
        if (!deleted)
            return NotFound(new ProblemDetails
            {
                Title = $"会话不存在：{conversationId}",
                Status = StatusCodes.Status404NotFound
            });

        _logger.LogInformation("Conversation reset: {ConversationId}", conversationId);
        return NoContent();
    }
}
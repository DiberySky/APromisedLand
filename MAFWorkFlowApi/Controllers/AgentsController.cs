using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace MAFWorkFlowApi.Controllers;

/// <summary>
/// 单 Agent 对话入口（支持持久化多轮会话）。
/// 路由：POST /api/agents/chat
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

    /// <summary>
    /// 向通用助手发送一条消息。
    /// 首次请求不传 conversationId，服务端返回新的 ID；
    /// 后续请求带上该 ID，Agent 将保留完整对话历史。
    /// </summary>
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
}
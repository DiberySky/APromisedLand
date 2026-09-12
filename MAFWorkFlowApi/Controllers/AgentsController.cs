using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace MAFWorkFlowApi.Controllers;

/// <summary>
/// 单 Agent 对话入口。
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

    /// <summary>向通用助手发送一条消息，得到 Ollama 本地模型的回复。</summary>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(AgentReply), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentReply>> Chat(
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Received chat request, length = {Len}", request.Message.Length);

        var reply = await _agents.ChatAsync(request.Message, ct);
        return Ok(reply);
    }
}
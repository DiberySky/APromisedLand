using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace MAFWorkFlowApi.Controllers;

/// <summary>
/// MAF Workflow 编排入口。
/// 路由：POST /api/workflows/writer-critic
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class WorkflowsController : ControllerBase
{
    private readonly MafAgentService _agents;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(
        MafAgentService agents,
        ILogger<WorkflowsController> logger)
    {
        _agents = agents;
        _logger = logger;
    }

    [HttpPost("writer-critic")]
    [ProducesResponseType(typeof(WorkflowReply), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowReply>> RunWriterCritic(
        [FromBody] WorkflowRunRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Running writer-critic workflow for topic: {Topic}", request.Topic);

        var reply = await _agents.RunWriterCriticAsync(request.Topic, ct);
        return Ok(reply);
    }
}
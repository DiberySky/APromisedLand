using System.Text;
using System.Text.Json;
using MafRagApi.Models;
using MafRagApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MafRagApi.Controllers;

/// <summary>
/// MAF Workflow 编排入口。
/// 路由前缀：/api/workflows
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class WorkflowsController : ControllerBase
{
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly WorkflowService _workflowService;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(
        WorkflowService workflowService,
        ILogger<WorkflowsController> logger)
    {
        _workflowService = workflowService;
        _logger = logger;
    }

    /// <summary>非流式运行 Writer-Critic 工作流。</summary>
    [HttpPost("writer-critic")]
    [ProducesResponseType(typeof(WorkflowRunResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowRunResponseDto>> RunWriterCritic(
        [FromBody] WorkflowRunRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Topic))
            return BadRequest(new { error = "topic is required." });

        _logger.LogInformation(
            "Running writer-critic workflow for topic: {Topic}", request.Topic);

        var reply = await _workflowService.RunWriterCriticAsync(request.Topic, ct);
        return Ok(reply);
    }

    /// <summary>
    /// 流式运行 Writer-Critic 工作流，返回 SSE 流。
    /// 每个事件形如：data: {"agent":"Writer","delta":"...","isFinal":false}
    /// </summary>
    [HttpPost("writer-critic/stream")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task StreamWriterCritic(
        [FromBody] WorkflowRunRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Topic))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync(
                $"data: {JsonSerializer.Serialize(new { error = "topic is required." }, SseJsonOptions)}\n\n",
                Encoding.UTF8, ct);
            return;
        }

        _logger.LogInformation(
            "Streaming writer-critic workflow for topic: {Topic}", request.Topic);

        // 显式设置 SSE 响应头
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers["Connection"] = "keep-alive";

        await Response.Body.FlushAsync(ct);

        try
        {
            await foreach (var evt in _workflowService
                .RunWriterCriticStreamAsync(request.Topic, ct)
                .WithCancellation(ct))
            {
                var json = JsonSerializer.Serialize(evt, SseJsonOptions);
                var payload = $"data: {json}\n\n";

                await Response.WriteAsync(payload, Encoding.UTF8, ct);
                await Response.Body.FlushAsync(ct);
            }

            // 显式发送结束事件
            await Response.WriteAsync("event: done\ndata: {}\n\n", Encoding.UTF8, ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // 客户端断开，静默退出
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流式工作流执行失败");

            try
            {
                var errJson = JsonSerializer.Serialize(
                    new { error = ex.Message }, SseJsonOptions);
                await Response.WriteAsync($"event: error\ndata: {errJson}\n\n",
                    Encoding.UTF8, CancellationToken.None);
                await Response.Body.FlushAsync(CancellationToken.None);
            }
            catch
            {
                // 忽略二次写入失败
            }
        }
    }
}

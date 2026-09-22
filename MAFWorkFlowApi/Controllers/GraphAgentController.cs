using System.Text.Json;
using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.Models;
using MAFWorkFlowApi.Models.Graph;
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

    [HttpPost("chat/stream")]
    [Produces("text/event-stream")]
    public async Task StreamChat(
        [FromBody] GraphAgentRequest request,
        CancellationToken ct)
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        await Response.Body.FlushAsync(ct);

        var sseOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        try
        {
            await foreach (var chunk in _agents.ChatStreamAsync(
                               request.ConversationId, request.Message, ct))
            {
                var payload = JsonSerializer.Serialize(chunk, sseOpts);
                await Response.WriteAsync($"data: {payload}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { /* 客户端断开 */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流式对话失败");
            try
            {
                var err = JsonSerializer.Serialize(
                    new { type = "error", message = ex.Message }, sseOpts);
                await Response.WriteAsync($"data: {err}\n\n", CancellationToken.None);
                await Response.Body.FlushAsync(CancellationToken.None);
            }
            catch { }
        }
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<GraphAgentSessionsReply>> ListSessions(CancellationToken ct)
    {
        var ids = await _agents.ListConversationsAsync(ct);
        var names = await _agents.GetAllDisplayNamesAsync(ct);

        var summaries = ids
            .Select(id => new SessionSummary(
                Id: id,
                DisplayName: names.TryGetValue(id, out var n) ? n : null))
            .ToList();

        return Ok(new GraphAgentSessionsReply(summaries));
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

    /// <summary>为会话设置显示名（空字符串表示恢复为默认 GUID 显示）。</summary>
    [HttpPost("sessions/{conversationId}/rename")]
    public async Task<IActionResult> RenameSession(
        [FromRoute] string conversationId,
        [FromBody] RenameSessionRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return BadRequest(new ProblemDetails
            {
                Title = "会话 ID 不能为空。",
                Status = StatusCodes.Status400BadRequest
            });

        await _agents.SetSessionDisplayNameAsync(
            conversationId, request.DisplayName ?? "", ct);

        return NoContent();
    }
}
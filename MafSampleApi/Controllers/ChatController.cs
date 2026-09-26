using System.Text.Encodings.Web;
using System.Text.Json;
using MafSampleApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MafSampleApi.Services;

namespace MafSampleApi.Controllers;

[ApiController]
[Route("api/chat")]
[Produces("application/json")]
public sealed class ChatController(
    IAgentFactory agentFactory,
    ISessionStore sessionStore,
    IOptions<AgentOptions> agentOptions,
    ILogger<ChatController> logger) : ControllerBase
{
    private readonly AgentOptions _agentOptions = agentOptions.Value;

    /// <summary>
    /// qwen3 thinking 模式关闭指令。
    /// 直接追加到用户消息末尾，绕过 MAF/Agent 层可能不传 system prompt 的问题。
    /// </summary>
    private const string NoThinkHint = " /no_think";

    private static readonly JsonSerializerOptions SseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ─── 非流式 ────────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<ChatResponseDto>> ChatAsync(
        [FromBody] ChatRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message is required." });

        var sessionId = ResolveSessionId(request.SessionId);
        var model = request.Model ?? _agentOptions.ChatModel;

        var entry = await sessionStore.GetOrCreateAsync(sessionId, model,
            m => agentFactory.GetAgent(m), ct);

        // ★ 关键：追加 /no_think
        var effectiveMessage = AppendNoThink(request.Message);

        logger.LogInformation(
            "Chat request: session={SessionId}, model={Model}, msgLen={Len}",
            sessionId, model, effectiveMessage.Length);

        var response = await entry.Agent.RunAsync(
            effectiveMessage,
            session: entry.Session,
            cancellationToken: ct);

        return Ok(new ChatResponseDto
        {
            SessionId = sessionId,
            Model = model,
            Content = response.Text ?? string.Empty
        });
    }

    // ─── 流式 SSE ─────────────────────────────────────────────
    [HttpPost("stream")]
    public async Task StreamAsync(
        [FromBody] ChatRequestDto request,
        CancellationToken ct)
    {
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers.Connection = "keep-alive";

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteRawEventAsync(new { error = "message is required." }, ct);
            return;
        }

        var sessionId = ResolveSessionId(request.SessionId);
        var model = request.Model ?? _agentOptions.ChatModel;

        var entry = await sessionStore.GetOrCreateAsync(sessionId, model,
            m => agentFactory.GetAgent(m), ct);

        await SendEventAsync(new StreamChunkDto
        {
            SessionId = sessionId, Model = model, Content = "", Done = false
        }, ct);

        // ★ 关键：追加 /no_think
        var effectiveMessage = AppendNoThink(request.Message);

        try
        {
            await foreach (var update in entry.Agent.RunStreamingAsync(
                               effectiveMessage,
                               session: entry.Session,
                               cancellationToken: ct))
            {
                if (ct.IsCancellationRequested) break;

                string? textDelta = update.Text;
                if (string.IsNullOrEmpty(textDelta)) continue;

                await SendEventAsync(new StreamChunkDto
                {
                    SessionId = sessionId,
                    Model = model,
                    Content = textDelta,
                    Done = false
                }, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Stream chat failed: session={SessionId}, model={Model}",
                sessionId, model);
            await SendEventAsync(new StreamChunkDto
            {
                SessionId = sessionId,
                Model = model,
                Content = $"[error] {ex.Message}",
                Done = true
            }, ct);
            return;
        }

        await SendEventAsync(new StreamChunkDto
        {
            SessionId = sessionId, Model = model, Content = "", Done = true
        }, ct);
    }

    // ─── 工具方法 ──────────────────────────────────────────────
    /// <summary>追加 /no_think；如果消息里已有则跳过。</summary>
    private static string AppendNoThink(string message)
    {
        if (message.Contains("/no_think", StringComparison.OrdinalIgnoreCase))
            return message;
        return message.TrimEnd() + NoThinkHint;
    }

    private static string ResolveSessionId(string? provided)
        => string.IsNullOrWhiteSpace(provided)
            ? Guid.NewGuid().ToString("N")
            : provided.Trim();

    private Task SendEventAsync(StreamChunkDto chunk, CancellationToken ct)
        => WriteRawEventAsync(chunk, ct);

    private async Task WriteRawEventAsync<T>(T payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, SseJson);
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
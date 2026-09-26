using System.Text;
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

// ─── 非流式（单轮，带整体时间预算）─────────────────────────
[HttpPost]
public async Task<ActionResult<ChatResponseDto>> ChatAsync(
    [FromBody] ChatRequestDto request,
    CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return BadRequest(new { error = "message is required." });

    var sessionId = ResolveSessionId(request.SessionId);
    var model     = request.Model ?? _agentOptions.ChatModel;

    var entry = await sessionStore.GetOrCreateAsync(sessionId, model,
        m => agentFactory.GetAgent(m), ct);

    var effectiveMessage = AppendNoThink(request.Message);

    // ★ 单轮时间预算：默认 120s，夹紧到 [15, 300]
    var budgetSeconds = Math.Clamp(_agentOptions.ChatBudgetSeconds, 15, 300);
    using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
    var chatCt = budgetCts.Token;

    logger.LogInformation(
        "Chat request: session={SessionId}, model={Model}, msgLen={Len}, budget={Budget}s",
        sessionId, model, effectiveMessage.Length, budgetSeconds);

    try
    {
        var response = await entry.Agent.RunAsync(
            effectiveMessage,
            session: entry.Session,
            cancellationToken: chatCt);

        return Ok(new ChatResponseDto
        {
            SessionId = sessionId,
            Model     = model,
            Content   = response.Text ?? string.Empty
        });
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        // 客户端断开：无接收方，静默退出
        logger.LogInformation(
            "Chat client disconnected: session={SessionId}, model={Model}",
            sessionId, model);
        return new EmptyResult();
    }
    catch (OperationCanceledException) when (chatCt.IsCancellationRequested)
    {
        // 时间预算耗尽
        logger.LogWarning(
            "Chat budget exhausted ({Budget}s): session={SessionId}, model={Model}",
            budgetSeconds, sessionId, model);
        return StatusCode(StatusCodes.Status504GatewayTimeout, new
        {
            error      = $"chat exceeded server budget of {budgetSeconds}s.",
            sessionId,
            model,
            budgetSeconds
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex,
            "Chat failed: session={SessionId}, model={Model}",
            sessionId, model);
        return StatusCode(StatusCodes.Status500InternalServerError,
            new { error = ex.Message, sessionId, model });
    }
}

    // ─── 流式（单轮）───────────────────────────────────────────
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
                    Model     = model,
                    Content   = textDelta,
                    Done      = false
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
                Model     = model,
                Content   = $"[error] {ex.Message}",
                Done      = true
            }, ct);
            return;
        }

        await SendEventAsync(new StreamChunkDto
        {
            SessionId = sessionId, Model = model, Content = "", Done = true
        }, ct);
    }

    // ─── 循环（流式 SSE）───────────────────────────────────────
    [HttpPost("loop")]
    public async Task StreamLoopAsync(
        [FromBody] LoopChatRequestDto request,
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
        var model     = request.Model ?? _agentOptions.ChatModel;
        var maxRounds = Math.Clamp(request.MaxRounds, 1, 20);

        var entry = await sessionStore.GetOrCreateAsync(sessionId, model,
            m => agentFactory.GetAgent(m), ct);

        logger.LogInformation(
            "Loop chat start: session={SessionId}, model={Model}, maxRounds={MaxRounds}",
            sessionId, model, maxRounds);

        await WriteRawEventAsync(new LoopStreamChunkDto
        {
            SessionId = sessionId, Model = model,
            Round = 0, MaxRounds = maxRounds,
            Phase = "start", Done = false,
        }, ct);

        var prompt   = AppendNoThink(request.Message);
        var finished = 0;

        for (var round = 1; round <= maxRounds && !ct.IsCancellationRequested; round++)
        {
            var sb = new StringBuilder();

            await WriteRawEventAsync(new LoopStreamChunkDto
            {
                SessionId = sessionId, Model = model,
                Round = round, MaxRounds = maxRounds,
                Phase = "round_start", Done = false,
            }, ct);

            try
            {
                await foreach (var update in entry.Agent.RunStreamingAsync(
                                   prompt,
                                   session: entry.Session,
                                   cancellationToken: ct))
                {
                    if (ct.IsCancellationRequested) break;

                    var delta = update.Text;
                    if (string.IsNullOrEmpty(delta)) continue;

                    sb.Append(delta);

                    await WriteRawEventAsync(new LoopStreamChunkDto
                    {
                        SessionId = sessionId, Model = model,
                        Round = round, MaxRounds = maxRounds,
                        Content = delta,
                        Phase = "delta", Done = false,
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
                    "Loop chat failed: session={SessionId}, model={Model}, round={Round}",
                    sessionId, model, round);

                await WriteRawEventAsync(new LoopStreamChunkDto
                {
                    SessionId = sessionId, Model = model,
                    Round = round, MaxRounds = maxRounds,
                    Content = $"[error] {ex.Message}",
                    Phase = "error", Done = true,
                }, ct);
                return;
            }

            finished = round;

            await WriteRawEventAsync(new LoopStreamChunkDto
            {
                SessionId = sessionId, Model = model,
                Round = round, MaxRounds = maxRounds,
                Phase = "round_end", Done = false,
            }, ct);

            if (round == maxRounds) break;

            prompt = BuildNextPrompt(request.ContinuePrompt, sb.ToString());
        }

        await WriteRawEventAsync(new LoopStreamChunkDto
        {
            SessionId = sessionId, Model = model,
            Round = finished, MaxRounds = maxRounds,
            Phase = "done", Done = true,
        }, ct);
    }

    // ─── 循环（非流式，带整体时间预算）─────────────────────────
    [HttpPost("loop/sync")]
    public async Task<ActionResult<LoopChatResponseDto>> LoopChatAsync(
        [FromBody] LoopChatRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message is required." });

        var sessionId = ResolveSessionId(request.SessionId);
        var model     = request.Model ?? _agentOptions.ChatModel;
        var maxRounds = Math.Clamp(request.MaxRounds, 1, 20);

        var entry = await sessionStore.GetOrCreateAsync(sessionId, model,
            m => agentFactory.GetAgent(m), ct);

        // ★ 总时间预算：默认 240s，夹紧到 [30, 600]
        var budgetSeconds = Math.Clamp(_agentOptions.LoopBudgetSeconds, 30, 600);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var loopCt = budgetCts.Token;

        logger.LogInformation(
            "Loop chat (sync) start: session={SessionId}, model={Model}, maxRounds={MaxRounds}, budget={Budget}s",
            sessionId, model, maxRounds, budgetSeconds);

        var prompt = AppendNoThink(request.Message);
        var result = new LoopChatResponseDto
        {
            SessionId = sessionId,
            Model     = model,
            MaxRounds = maxRounds,
        };

        var truncated = false;

        for (var round = 1; round <= maxRounds; round++)
        {
            if (loopCt.IsCancellationRequested)
            {
                truncated = true;
                logger.LogWarning(
                    "Loop chat (sync) budget exhausted before round {Round}: session={SessionId}",
                    round, sessionId);
                break;
            }

            try
            {
                var response = await entry.Agent.RunAsync(
                    prompt,
                    session: entry.Session,
                    cancellationToken: loopCt);

                var text = response.Text ?? string.Empty;

                result.Rounds.Add(new LoopRoundDto
                {
                    Round   = round,
                    Content = text,
                });

                logger.LogInformation(
                    "Loop chat round {Round}/{MaxRounds} done: session={SessionId}, len={Len}",
                    round, maxRounds, sessionId, text.Length);

                if (round == maxRounds) break;

                prompt = BuildNextPrompt(request.ContinuePrompt, text);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // 客户端断开连接：无接收方，直接结束
                logger.LogInformation(
                    "Loop chat (sync) client disconnected: session={SessionId}, round={Round}",
                    sessionId, round);
                return new EmptyResult();
            }
            catch (OperationCanceledException) when (loopCt.IsCancellationRequested)
            {
                // 时间预算耗尽：把已完成轮次正常返回
                truncated = true;
                logger.LogWarning(
                    "Loop chat (sync) budget exhausted at round {Round}: session={SessionId}",
                    round, sessionId);
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Loop chat (sync) failed: session={SessionId}, model={Model}, round={Round}",
                    sessionId, model, round);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = ex.Message, sessionId, model, round });
            }
        }

        result.CompletedRounds = result.Rounds.Count;
        result.Truncated       = truncated;
        result.Content         = result.Rounds.Count > 0
            ? result.Rounds[^1].Content
            : string.Empty;

        return Ok(result);
    }

    // ─── 工具方法 ──────────────────────────────────────────────

    /// <summary>追加 /no_think；如果消息里已有则跳过。</summary>
    private static string AppendNoThink(string message)
    {
        if (message.Contains("/no_think", StringComparison.OrdinalIgnoreCase))
            return message;
        return message.TrimEnd() + NoThinkHint;
    }

    /// <summary>
    /// 根据 ContinuePrompt 模板生成下一轮输入；
    /// 没有模板时默认让 Agent 自行延续。
    /// </summary>
    private static string BuildNextPrompt(string? continuePrompt, string previousAnswer)
    {
        var text = string.IsNullOrWhiteSpace(continuePrompt)
            ? "请继续扩展并完善上一条回答。"
            : (continuePrompt.Contains("{previous}", StringComparison.Ordinal)
                ? continuePrompt.Replace("{previous}", previousAnswer, StringComparison.Ordinal)
                : continuePrompt);

        return AppendNoThink(text);
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
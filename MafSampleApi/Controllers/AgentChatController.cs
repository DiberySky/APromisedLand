using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MafSampleApi.Models;
using MafSampleApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MafSampleApi.Controllers;

/// <summary>
/// MAF Agent + vLLM 增强版 Chat。
///
/// 特点：
/// - 有会话上下文（走 AgentSessionStore）
/// - 请求级可覆盖 system prompt / 采样参数 / 指令模板
/// - 支持 responseSchema 校验 + 自动重试
/// - 与 /api/chat、/api/vllm/chat 完全独立
///
/// 路由：
///   POST /api/agent/chat            —— 非流式
///   POST /api/agent/chat/stream     —— 流式（SSE）
///   POST /api/agent/chat/loop       —— 循环流式（SSE）
///   POST /api/agent/chat/loop/sync  —— 循环非流式
/// </summary>
[ApiController]
[Route("api/agent/chat")]
[Produces("application/json")]
public sealed class AgentChatController(
    AgentSessionStore sessionStore,
    IInstructionTemplateStore templates,
    IOptions<AgentOptions> agentOptions,
    ILogger<AgentChatController> logger) : ControllerBase
{
    private readonly AgentOptions _options = agentOptions.Value;

    private const string NoThinkHint = " /no_think";
    private const int DefaultMaxOutputTokens = 2048;
    private const int MaxRoundsLimit = 20;
    private const int MaxSchemaRetriesLimit = 5;

    private static readonly JsonSerializerOptions SseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ═══════════════════════════════════════════════════════════
    //  非流式
    // ═══════════════════════════════════════════════════════════
    [HttpPost]
    public async Task<ActionResult<AgentChatResponseDto>> ChatAsync(
        [FromBody] AgentChatRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message is required." });

        if (!TryResolveInstruction(request, out var resolved, out var resolveError))
        {
            return resolveError!.Value.status == 404
                ? NotFound(new { error = resolveError.Value.msg })
                : BadRequest(new { error = resolveError.Value.msg });
        }

        var sessionId = ResolveSessionId(request.SessionId);
        var model     = ResolveModel(request.Model);
        var sysText   = BuildSystemText(resolved.instruction, resolved.outputFormat);
        var chatOpts  = BuildChatOptions(model, request);

        var entry = await sessionStore.GetOrCreateAsync(
            sessionId, model, sysText, chatOpts, ct);

        var budgetSeconds = Math.Clamp(_options.ChatBudgetSeconds, 15, 300);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var chatCt = budgetCts.Token;

        var maxAttempts = 1 + Math.Clamp(request.MaxRetries, 0, MaxSchemaRetriesLimit);
        var needSchema  = !string.IsNullOrWhiteSpace(request.ResponseSchema);

        logger.LogInformation(
            "Agent chat: session={Sid}, model={M}, tmpl={T}, schema={Sc}, attempts={A}, budget={B}s",
            sessionId, model, request.InstructionId ?? "-",
            needSchema ? "yes" : "no", maxAttempts, budgetSeconds);

        try
        {
            string userMsg = AppendNoThink(request.Message);
            string? schemaErr = null;
            string content = string.Empty;
            int attempt = 0;

            for (; attempt < maxAttempts; attempt++)
            {
                var response = await entry.Agent.RunAsync(
                    userMsg,
                    session: entry.Session,
                    cancellationToken: chatCt);

                content = response.Text;

                if (!needSchema) break;

                if (JsonSchemaValidator.TryValidate(
                        content, request.ResponseSchema!, out var err))
                {
                    schemaErr = null;
                    break;
                }

                schemaErr = err;
                logger.LogWarning(
                    "Agent chat schema failed (attempt {A}/{T}): {Err}",
                    attempt + 1, maxAttempts, err);

                // 还有重试机会 → 把错误喂回会话，让 Agent 修正
                if (attempt < maxAttempts - 1)
                {
                    userMsg =
                        $"你上一次的输出不符合 JSON Schema。错误：{err}。" +
                        "请严格按 Schema 重新输出，只输出 JSON，不要任何解释或代码块围栏。";
                }
            }

            return Ok(new AgentChatResponseDto
            {
                SessionId   = sessionId,
                Model       = model,
                Content     = content,
                Truncated   = false,
                Attempts    = attempt + 1,
                SchemaValid = schemaErr is null,
                SchemaError = schemaErr,
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (OperationCanceledException) when (budgetCts.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                error = $"agent chat exceeded budget of {budgetSeconds}s.",
                sessionId, model, budgetSeconds,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Agent chat failed: session={Sid}, model={M}", sessionId, model);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = ex.Message, sessionId, model });
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  流式
    // ═══════════════════════════════════════════════════════════
    [HttpPost("stream")]
    public async Task StreamAsync(
        [FromBody] AgentChatRequestDto request,
        CancellationToken ct)
    {
        SetSseHeaders();

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteEventAsync(new { error = "message is required." }, ct);
            return;
        }

        if (!TryResolveInstruction(request, out var resolved, out var resolveError))
        {
            Response.StatusCode = resolveError!.Value.status;
            await WriteEventAsync(new { error = resolveError.Value.msg }, ct);
            return;
        }

        var sessionId = ResolveSessionId(request.SessionId);
        var model     = ResolveModel(request.Model);
        var sysText   = BuildSystemText(resolved.instruction, resolved.outputFormat);
        var chatOpts  = BuildChatOptions(model, request);

        var entry = await sessionStore.GetOrCreateAsync(
            sessionId, model, sysText, chatOpts, ct);

        var budgetSeconds = Math.Clamp(_options.ChatBudgetSeconds, 15, 300);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var chatCt = budgetCts.Token;

        logger.LogInformation(
            "Agent chat (stream): session={Sid}, model={M}, budget={B}s",
            sessionId, model, budgetSeconds);

        await WriteEventAsync(new AgentStreamChunkDto
        {
            SessionId = sessionId, Model = model, Content = "", Done = false
        }, ct);

        var sb        = new StringBuilder();
        var truncated = false;

        try
        {
            var userMsg = AppendNoThink(request.Message);

            await foreach (var update in entry.Agent.RunStreamingAsync(
                               userMsg,
                               session: entry.Session,
                               cancellationToken: chatCt))
            {
                if (chatCt.IsCancellationRequested) break;

                var delta = update.Text;
                if (string.IsNullOrEmpty(delta)) continue;
                sb.Append(delta);

                await WriteEventAsync(new AgentStreamChunkDto
                {
                    SessionId = sessionId, Model = model,
                    Content = delta, Done = false,
                }, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (OperationCanceledException) when (budgetCts.IsCancellationRequested)
        {
            truncated = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Agent chat (stream) failed: session={Sid}", sessionId);
            await WriteEventAsync(new AgentStreamChunkDto
            {
                SessionId = sessionId, Model = model,
                Content = $"[error] {ex.Message}", Done = true,
            }, ct);
            return;
        }

        var schemaValid = true;
        string? schemaErr = null;
        if (!truncated && !string.IsNullOrWhiteSpace(request.ResponseSchema))
        {
            schemaValid = JsonSchemaValidator.TryValidate(
                sb.ToString(), request.ResponseSchema, out schemaErr);
        }

        await WriteEventAsync(new AgentStreamChunkDto
        {
            SessionId   = sessionId,
            Model       = model,
            Content     = "",
            Done        = true,
            Truncated   = truncated,
            SchemaValid = schemaValid,
            SchemaError = schemaErr,
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════
    //  循环 —— 流式
    // ═══════════════════════════════════════════════════════════
    [HttpPost("loop")]
    public async Task StreamLoopAsync(
        [FromBody] AgentLoopRequestDto request,
        CancellationToken ct)
    {
        SetSseHeaders();

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteEventAsync(new { error = "message is required." }, ct);
            return;
        }

        if (!TryResolveInstruction(request, out var resolved, out var resolveError))
        {
            Response.StatusCode = resolveError!.Value.status;
            await WriteEventAsync(new { error = resolveError.Value.msg }, ct);
            return;
        }

        var sessionId = ResolveSessionId(request.SessionId);
        var model     = ResolveModel(request.Model);
        var sysText   = BuildSystemText(resolved.instruction, resolved.outputFormat);
        var chatOpts  = BuildChatOptions(model, request);
        var maxRounds = Math.Clamp(request.MaxRounds, 1, MaxRoundsLimit);

        var entry = await sessionStore.GetOrCreateAsync(
            sessionId, model, sysText, chatOpts, ct);

        var budgetSeconds = Math.Clamp(_options.LoopBudgetSeconds, 30, 600);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var loopCt = budgetCts.Token;

        logger.LogInformation(
            "Agent loop (stream): session={Sid}, model={M}, maxRounds={R}, budget={B}s",
            sessionId, model, maxRounds, budgetSeconds);

        await WriteEventAsync(new LoopStreamChunkDto
        {
            SessionId = sessionId, Model = model,
            Round = 0, MaxRounds = maxRounds, Phase = "start", Done = false,
        }, ct);

        var prompt    = AppendNoThink(request.Message);
        var finished  = 0;
        var truncated = false;

        for (var round = 1; round <= maxRounds && !ct.IsCancellationRequested; round++)
        {
            if (loopCt.IsCancellationRequested) { truncated = true; break; }

            var sb = new StringBuilder();

            await WriteEventAsync(new LoopStreamChunkDto
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
                                   cancellationToken: loopCt))
                {
                    if (loopCt.IsCancellationRequested) break;

                    var delta = update.Text;
                    if (string.IsNullOrEmpty(delta)) continue;
                    sb.Append(delta);

                    await WriteEventAsync(new LoopStreamChunkDto
                    {
                        SessionId = sessionId, Model = model,
                        Round = round, MaxRounds = maxRounds,
                        Content = delta, Phase = "delta", Done = false,
                    }, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (OperationCanceledException) when (budgetCts.IsCancellationRequested)
            {
                truncated = true; break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Agent loop failed: session={Sid}, round={R}", sessionId, round);
                await WriteEventAsync(new LoopStreamChunkDto
                {
                    SessionId = sessionId, Model = model,
                    Round = round, MaxRounds = maxRounds,
                    Content = $"[error] {ex.Message}",
                    Phase = "error", Done = true,
                }, ct);
                return;
            }

            if (truncated) break;

            var text = sb.ToString();
            finished = round;

            await WriteEventAsync(new LoopStreamChunkDto
            {
                SessionId = sessionId, Model = model,
                Round = round, MaxRounds = maxRounds,
                Phase = "round_end", Done = false,
            }, ct);

            if (round == maxRounds) break;

            prompt = BuildNextPrompt(request.ContinuePrompt, text);
        }

        await WriteEventAsync(new LoopStreamChunkDto
        {
            SessionId = sessionId, Model = model,
            Round = finished, MaxRounds = maxRounds,
            Phase = "done", Done = true, Truncated = truncated,
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════
    //  循环 —— 非流式
    // ═══════════════════════════════════════════════════════════
    [HttpPost("loop/sync")]
    public async Task<ActionResult<LoopChatResponseDto>> LoopSyncAsync(
        [FromBody] AgentLoopRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message is required." });

        if (!TryResolveInstruction(request, out var resolved, out var resolveError))
        {
            return resolveError!.Value.status == 404
                ? NotFound(new { error = resolveError.Value.msg })
                : BadRequest(new { error = resolveError.Value.msg });
        }

        var sessionId = ResolveSessionId(request.SessionId);
        var model     = ResolveModel(request.Model);
        var sysText   = BuildSystemText(resolved.instruction, resolved.outputFormat);
        var chatOpts  = BuildChatOptions(model, request);
        var maxRounds = Math.Clamp(request.MaxRounds, 1, MaxRoundsLimit);

        var entry = await sessionStore.GetOrCreateAsync(
            sessionId, model, sysText, chatOpts, ct);

        var budgetSeconds = Math.Clamp(_options.LoopBudgetSeconds, 30, 600);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var loopCt = budgetCts.Token;

        var rounds    = new List<LoopRoundDto>(maxRounds);
        var prompt    = AppendNoThink(request.Message);
        var truncated = false;

        for (var round = 1; round <= maxRounds; round++)
        {
            if (ct.IsCancellationRequested) return new EmptyResult();
            if (loopCt.IsCancellationRequested) { truncated = true; break; }

            try
            {
                var response = await entry.Agent.RunAsync(
                    prompt, session: entry.Session, cancellationToken: loopCt);

                var text = response.Text;
                rounds.Add(new LoopRoundDto { Round = round, Content = text });

                if (round == maxRounds) break;
                prompt = BuildNextPrompt(request.ContinuePrompt, text);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (OperationCanceledException) when (budgetCts.IsCancellationRequested)
            {
                truncated = true; break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Agent loop (sync) failed: session={Sid}, round={R}", sessionId, round);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = ex.Message, sessionId, round });
            }
        }

        return Ok(new LoopChatResponseDto
        {
            SessionId       = sessionId,
            Model           = model,
            MaxRounds       = maxRounds,
            CompletedRounds = rounds.Count,
            Truncated       = truncated,
            Rounds          = rounds,
            Content         = rounds.Count > 0 ? rounds[^1].Content : string.Empty,
        });
    }

    // ═══════════════════════════════════════════════════════════
    //  工具
    // ═══════════════════════════════════════════════════════════

    private static string ResolveSessionId(string? provided)
        => string.IsNullOrWhiteSpace(provided)
            ? Guid.NewGuid().ToString("N")
            : provided.Trim();

    private string ResolveModel(string? requested)
        => string.IsNullOrWhiteSpace(requested) ? _options.ChatModel : requested;

    /// <summary>解析指令来源（模板 / 内联 / 默认）。</summary>
    private bool TryResolveInstruction(
        AgentChatRequestDto request,
        out (string instruction, IReadOnlyList<InstructExampleDto>? examples, string? outputFormat) resolved,
        out (int status, string msg)? error)
    {
        resolved = default;
        error    = null;

        InstructionTemplate? tpl = null;
        if (!string.IsNullOrWhiteSpace(request.InstructionId))
        {
            if (!templates.TryGet(request.InstructionId, out tpl))
            {
                error = (404, $"instructionId '{request.InstructionId}' not found.");
                return false;
            }
        }

        var instruction = !string.IsNullOrWhiteSpace(request.Instruction)
            ? request.Instruction!
            : tpl?.Instruction;

        if (string.IsNullOrWhiteSpace(instruction))
            instruction = _options.SystemPrompt;

        var examples = request.Examples ?? tpl?.Examples;
        var fmt      = !string.IsNullOrWhiteSpace(request.OutputFormat)
            ? request.OutputFormat
            : tpl?.OutputFormat;

        resolved = (instruction, examples, fmt);
        return true;
    }

    private static bool TryResolveInstruction(
        AgentLoopRequestDto request,
        out (string instruction, IReadOnlyList<InstructExampleDto>? examples, string? outputFormat) resolved,
        out (int status, string msg)? error)
    {
        // 复用 Chat 版逻辑
        var asChat = new AgentChatRequestDto
        {
            InstructionId = request.InstructionId,
            Instruction   = request.Instruction,
            Examples      = request.Examples,
            OutputFormat  = request.OutputFormat,
        };
        return TryResolveInstructionStatic(asChat, out resolved, out error);
    }

    private static bool TryResolveInstructionStatic(
        AgentChatRequestDto request,
        out (string instruction, IReadOnlyList<InstructExampleDto>? examples, string? outputFormat) resolved,
        out (int status, string msg)? error)
    {
        // 简化版：只走内联 + 模板，默认由调用方填
        resolved = default;
        error    = null;

        var instruction = request.Instruction;
        if (string.IsNullOrWhiteSpace(instruction))
        {
            error = (400, "instruction or instructionId is required for loop endpoints.");
            return false;
        }

        resolved = (instruction, request.Examples, request.OutputFormat);
        return true;
    }

    /// <summary>系统提示词 = instruction (+ JSON 规范)。</summary>
    private static string BuildSystemText(string instruction, string? outputFormat)
    {
        var text = instruction.Trim();
        if (string.Equals(outputFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            text += "\n\n只输出合法 JSON，不要任何解释、代码块围栏或前后缀。";
        }
        return text;
    }

    private static ChatOptions BuildChatOptions(string model, AgentChatRequestDto r)
        => new()
        {
            ModelId         = model,
            Temperature     = r.Temperature,
            TopP            = r.TopP,
            Seed            = r.Seed,
            StopSequences   = r.Stop is { Count: > 0 } ? r.Stop.ToList() : null,
            MaxOutputTokens = r.MaxOutputTokens is > 0
                                ? r.MaxOutputTokens.Value
                                : DefaultMaxOutputTokens,
        };

    private static ChatOptions BuildChatOptions(string model, AgentLoopRequestDto r)
        => new()
        {
            ModelId         = model,
            Temperature     = r.Temperature,
            TopP            = r.TopP,
            Seed            = r.Seed,
            StopSequences   = r.Stop is { Count: > 0 } ? r.Stop.ToList() : null,
            MaxOutputTokens = r.MaxOutputTokens is > 0
                                ? r.MaxOutputTokens.Value
                                : DefaultMaxOutputTokens,
        };

    private static string BuildNextPrompt(string? continuePrompt, string previousAnswer)
    {
        var text = string.IsNullOrWhiteSpace(continuePrompt)
            ? "请继续扩展并完善上一条回答。"
            : (continuePrompt.Contains("{previous}", StringComparison.Ordinal)
                ? continuePrompt.Replace("{previous}", previousAnswer, StringComparison.Ordinal)
                : continuePrompt);

        return AppendNoThink(text);
    }

    private static string AppendNoThink(string message)
    {
        if (message.Contains("/no_think", StringComparison.OrdinalIgnoreCase))
            return message;
        return message.TrimEnd() + NoThinkHint;
    }

    private void SetSseHeaders()
    {
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers.Connection = "keep-alive";
    }

    private async Task WriteEventAsync<T>(T payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, SseJson);
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
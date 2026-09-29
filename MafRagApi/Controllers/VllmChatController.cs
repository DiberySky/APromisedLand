using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MafRagApi.Models;
using MafRagApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MafRagApi.Controllers;

/// <summary>
/// vLLM（Qwen3-4B-AWQ）专用聊天端点。
///
/// 路由：
///   POST /api/vllm/chat                    —— 非流式
///   POST /api/vllm/chat/stream             —— 流式（SSE）
///   POST /api/vllm/chat/loop               —— 循环流式（SSE）
///   POST /api/vllm/chat/loop/sync          —— 循环非流式
///   POST /api/vllm/chat/instruct           —— 指令式（非流式，支持 schema 校验 + 重试）
///   POST /api/vllm/chat/instruct/stream    —— 指令式（流式）
/// </summary>
[ApiController]
[Route("api/vllm/chat")]
[Produces("application/json")]
public sealed class VllmChatController(
    IChatClient chatClient,
    IOptions<AgentOptions> agentOptions,
    IInstructionTemplateStore templates,
    ILogger<VllmChatController> logger) : ControllerBase
{
    private readonly AgentOptions _options = agentOptions.Value;

    private const string NoThinkHint = " /no_think";
    private const int DefaultMaxOutputTokens = 512;
    private const int MaxRoundsLimit = 20;
    private const int MaxSchemaRetriesLimit = 5;

    private static readonly JsonSerializerOptions SseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ═══════════════════════════════════════════════════════════
    //  非流式（单轮）
    // ═══════════════════════════════════════════════════════════
    [HttpPost]
    public async Task<ActionResult<VllmChatResponseDto>> ChatAsync(
        [FromBody] VllmChatRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message is required." });

        var (model, messages, options) = BuildChatInput(
            request.Message, request.Model, request.SystemPrompt, request.MaxOutputTokens);

        var budgetSeconds = Math.Clamp(_options.VllmChatBudgetSeconds, 5, 600);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var chatCt = budgetCts.Token;

        logger.LogInformation(
            "vLLM chat: model={Model}, msgLen={Len}, maxTokens={Max}, budget={Budget}s",
            model, messages[^1].Text.Length, options.MaxOutputTokens, budgetSeconds);

        try
        {
            var response = await chatClient.GetResponseAsync(messages, options, chatCt);

            return Ok(new VllmChatResponseDto
            {
                Model     = model,
                Content   = response.Text,
                Truncated = false,
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
                error = $"vLLM chat exceeded server budget of {budgetSeconds}s.",
                model, budgetSeconds,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "vLLM chat failed: model={Model}", model);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = ex.Message, model });
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  流式（单轮）
    // ═══════════════════════════════════════════════════════════
    [HttpPost("stream")]
    public async Task StreamAsync(
        [FromBody] VllmChatRequestDto request,
        CancellationToken ct)
    {
        SetSseHeaders();

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteEventAsync(new { error = "message is required." }, ct);
            return;
        }

        var (model, messages, options) = BuildChatInput(
            request.Message, request.Model, request.SystemPrompt, request.MaxOutputTokens);

        var budgetSeconds = Math.Clamp(_options.VllmChatBudgetSeconds, 5, 600);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var chatCt = budgetCts.Token;

        await WriteEventAsync(new VllmStreamChunkDto
        {
            Model = model, Content = "", Done = false
        }, ct);

        var truncated = false;

        try
        {
            await foreach (var update in chatClient.GetStreamingResponseAsync(
                               messages, options, chatCt))
            {
                if (chatCt.IsCancellationRequested) break;
                var delta = update.Text;
                if (string.IsNullOrEmpty(delta)) continue;

                await WriteEventAsync(new VllmStreamChunkDto
                {
                    Model = model, Content = delta, Done = false,
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
            logger.LogError(ex, "vLLM chat (stream) failed: model={Model}", model);
            await WriteEventAsync(new VllmStreamChunkDto
            {
                Model = model, Content = $"[error] {ex.Message}", Done = true,
            }, ct);
            return;
        }

        await WriteEventAsync(new VllmStreamChunkDto
        {
            Model = model, Content = "", Done = true, Truncated = truncated,
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════
    //  循环 —— 流式 SSE
    // ═══════════════════════════════════════════════════════════
    [HttpPost("loop")]
    public async Task StreamLoopAsync(
        [FromBody] VllmLoopRequestDto request,
        CancellationToken ct)
    {
        SetSseHeaders();

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteEventAsync(new { error = "message is required." }, ct);
            return;
        }

        var model        = ResolveModel(request.Model);
        var systemPrompt = ResolveSystemPrompt(request.SystemPrompt);
        var maxRounds    = Math.Clamp(request.MaxRounds, 1, MaxRoundsLimit);
        var maxTokens    = request.MaxOutputTokens is > 0
            ? request.MaxOutputTokens.Value
            : DefaultMaxOutputTokens;

        var chatOptions = new ChatOptions
        {
            ModelId         = model,
            MaxOutputTokens = maxTokens,
        };

        var budgetSeconds = Math.Clamp(_options.VllmLoopBudgetSeconds, 10, 900);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var loopCt = budgetCts.Token;

        var history = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User,   AppendNoThink(request.Message)),
        };

        await WriteEventAsync(new VllmLoopStreamChunkDto
        {
            Model = model, Round = 0, MaxRounds = maxRounds,
            Phase = "start", Done = false,
        }, ct);

        var finished  = 0;
        var truncated = false;

        for (var round = 1; round <= maxRounds && !ct.IsCancellationRequested; round++)
        {
            if (loopCt.IsCancellationRequested) { truncated = true; break; }

            var sb = new StringBuilder();

            await WriteEventAsync(new VllmLoopStreamChunkDto
            {
                Model = model, Round = round, MaxRounds = maxRounds,
                Phase = "round_start", Done = false,
            }, ct);

            try
            {
                await foreach (var update in chatClient.GetStreamingResponseAsync(
                                   history, chatOptions, loopCt))
                {
                    if (loopCt.IsCancellationRequested) break;
                    var delta = update.Text;
                    if (string.IsNullOrEmpty(delta)) continue;
                    sb.Append(delta);

                    await WriteEventAsync(new VllmLoopStreamChunkDto
                    {
                        Model = model, Round = round, MaxRounds = maxRounds,
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
                    "vLLM loop (stream) failed: model={Model}, round={Round}", model, round);
                await WriteEventAsync(new VllmLoopStreamChunkDto
                {
                    Model = model, Round = round, MaxRounds = maxRounds,
                    Content = $"[error] {ex.Message}", Phase = "error", Done = true,
                }, ct);
                return;
            }

            if (truncated) break;

            var text = sb.ToString();
            finished = round;

            await WriteEventAsync(new VllmLoopStreamChunkDto
            {
                Model = model, Round = round, MaxRounds = maxRounds,
                Phase = "round_end", Done = false,
            }, ct);

            if (round == maxRounds) break;
            PrepareNextTurn(history, systemPrompt, text,
                request.ContinuePrompt, request.KeepHistory);
        }

        await WriteEventAsync(new VllmLoopStreamChunkDto
        {
            Model = model, Round = finished, MaxRounds = maxRounds,
            Phase = "done", Done = true, Truncated = truncated,
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════
    //  循环 —— 非流式
    // ═══════════════════════════════════════════════════════════
    [HttpPost("loop/sync")]
    public async Task<ActionResult<VllmLoopResponseDto>> LoopChatAsync(
        [FromBody] VllmLoopRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message is required." });

        var model        = ResolveModel(request.Model);
        var systemPrompt = ResolveSystemPrompt(request.SystemPrompt);
        var maxRounds    = Math.Clamp(request.MaxRounds, 1, MaxRoundsLimit);
        var maxTokens    = request.MaxOutputTokens is > 0
            ? request.MaxOutputTokens.Value
            : DefaultMaxOutputTokens;

        var chatOptions = new ChatOptions
        {
            ModelId         = model,
            MaxOutputTokens = maxTokens,
        };

        var budgetSeconds = Math.Clamp(_options.VllmLoopBudgetSeconds, 10, 900);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var loopCt = budgetCts.Token;

        var history = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User,   AppendNoThink(request.Message)),
        };

        var rounds    = new List<VllmLoopRoundDto>(maxRounds);
        var truncated = false;

        for (var round = 1; round <= maxRounds; round++)
        {
            if (ct.IsCancellationRequested) return new EmptyResult();
            if (loopCt.IsCancellationRequested) { truncated = true; break; }

            try
            {
                var response = await chatClient.GetResponseAsync(history, chatOptions, loopCt);
                var text = response.Text;

                rounds.Add(new VllmLoopRoundDto { Round = round, Content = text });

                if (round == maxRounds) break;
                PrepareNextTurn(history, systemPrompt, text,
                    request.ContinuePrompt, request.KeepHistory);
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
                    "vLLM loop (sync) failed: model={Model}, round={Round}", model, round);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = ex.Message, model, round });
            }
        }

        return Ok(new VllmLoopResponseDto
        {
            Model           = model,
            MaxRounds       = maxRounds,
            CompletedRounds = rounds.Count,
            Truncated       = truncated,
            Rounds          = rounds,
            Content         = rounds.Count > 0 ? rounds[^1].Content : string.Empty,
        });
    }

    // ═══════════════════════════════════════════════════════════
    //  指令式 Chat —— 非流式（带 schema 校验 + 自动重试）
    // ═══════════════════════════════════════════════════════════
    [HttpPost("instruct")]
    public async Task<ActionResult<InstructChatResponseDto>> InstructAsync(
        [FromBody] InstructChatRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message is required." });

        // 解析指令来源（模板 / 内联）
        if (!TryResolveInstruction(request, out var resolved, out var resolveError))
        {
            if (resolveError!.Value.status == 404)
                return NotFound(new { error = resolveError.Value.msg });
            return BadRequest(new { error = resolveError.Value.msg });
        }

        var (model, messages, options) = BuildInstructInput(
            resolved.instruction, resolved.examples, resolved.outputFormat, request);

        var budgetSeconds = Math.Clamp(_options.VllmChatBudgetSeconds, 5, 600);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var chatCt = budgetCts.Token;

        var maxAttempts = 1 + Math.Clamp(request.MaxRetries, 0, MaxSchemaRetriesLimit);
        var needSchema  = !string.IsNullOrWhiteSpace(request.ResponseSchema);

        logger.LogInformation(
            "vLLM instruct: model={Model}, tmpl={Tmpl}, msgLen={Len}, fmt={Fmt}, " +
            "schema={Schema}, maxAttempts={A}, budget={B}s",
            model, request.InstructionId ?? "-", request.Message.Length,
            resolved.outputFormat ?? "text",
            needSchema ? "yes" : "no", maxAttempts, budgetSeconds);

        try
        {
            ChatResponse?  lastResp   = null;
            string?        schemaErr  = null;
            int            attempt    = 0;

            for (; attempt < maxAttempts; attempt++)
            {
                lastResp = await chatClient.GetResponseAsync(messages, options, chatCt);

                if (!needSchema) break;

                if (JsonSchemaValidator.TryValidate(
                        lastResp.Text, request.ResponseSchema!, out var err))
                {
                    schemaErr = null;
                    break;
                }

                schemaErr = err;
                logger.LogWarning(
                    "vLLM instruct schema failed (attempt {A}/{T}): {Err}",
                    attempt + 1, maxAttempts, err);

                // 还能重试 → 把模型输出 + 错误提示追加进对话
                if (attempt < maxAttempts - 1)
                {
                    messages.Add(new ChatMessage(ChatRole.Assistant, lastResp.Text));
                    messages.Add(new ChatMessage(ChatRole.User,
                        $"你上一次的输出不符合 JSON Schema。错误：{err}。" +
                        "请严格按 Schema 重新输出，只输出 JSON，不要任何解释或代码块围栏。"));
                }
            }

            return Ok(new InstructChatResponseDto
            {
                Model       = model,
                Content     = lastResp!.Text,
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
                error = $"vLLM instruct exceeded budget of {budgetSeconds}s.",
                model, budgetSeconds,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "vLLM instruct failed: model={Model}", model);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = ex.Message, model });
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  指令式 Chat —— 流式（末尾附加 schema 校验结果，不自动重试）
    // ═══════════════════════════════════════════════════════════
    [HttpPost("instruct/stream")]
    public async Task InstructStreamAsync(
        [FromBody] InstructChatRequestDto request,
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

        var (model, messages, options) = BuildInstructInput(
            resolved.instruction, resolved.examples, resolved.outputFormat, request);

        var budgetSeconds = Math.Clamp(_options.VllmChatBudgetSeconds, 5, 600);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var chatCt = budgetCts.Token;

        logger.LogInformation(
            "vLLM instruct (stream): model={Model}, tmpl={Tmpl}, fmt={Fmt}, budget={B}s",
            model, request.InstructionId ?? "-", resolved.outputFormat ?? "text", budgetSeconds);

        await WriteEventAsync(new InstructStreamChunkDto
        {
            Model = model, Content = "", Done = false
        }, ct);

        var sb        = new StringBuilder();
        var truncated = false;

        try
        {
            await foreach (var update in chatClient.GetStreamingResponseAsync(
                               messages, options, chatCt))
            {
                if (chatCt.IsCancellationRequested) break;
                var delta = update.Text;
                if (string.IsNullOrEmpty(delta)) continue;
                sb.Append(delta);

                await WriteEventAsync(new InstructStreamChunkDto
                {
                    Model = model, Content = delta, Done = false,
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
            logger.LogError(ex, "vLLM instruct (stream) failed: model={Model}", model);
            await WriteEventAsync(new InstructStreamChunkDto
            {
                Model = model, Content = $"[error] {ex.Message}", Done = true,
            }, ct);
            return;
        }

        // 流式不做重试，只在校验失败时附上 SchemaError 让客户端决定
        var schemaValid = true;
        string? schemaErr = null;
        if (!truncated && !string.IsNullOrWhiteSpace(request.ResponseSchema))
        {
            schemaValid = JsonSchemaValidator.TryValidate(
                sb.ToString(), request.ResponseSchema, out schemaErr);
        }

        await WriteEventAsync(new InstructStreamChunkDto
        {
            Model       = model,
            Content     = "",
            Done        = true,
            Truncated   = truncated,
            SchemaValid = schemaValid,
            SchemaError = schemaErr,
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════
    //  工具
    // ═══════════════════════════════════════════════════════════

    private string ResolveModel(string? requested)
        => string.IsNullOrWhiteSpace(requested) ? _options.ChatModel : requested;

    private string ResolveSystemPrompt(string? requested)
        => string.IsNullOrWhiteSpace(requested) ? _options.SystemPrompt : requested;

    private (string Model, List<ChatMessage> Messages, ChatOptions Options) BuildChatInput(
        string message, string? model, string? systemPrompt, int? maxOutputTokens)
    {
        var resolvedModel  = ResolveModel(model);
        var resolvedSystem = ResolveSystemPrompt(systemPrompt);
        var userMessage    = AppendNoThink(message);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, resolvedSystem),
            new(ChatRole.User,   userMessage),
        };

        var tokens = maxOutputTokens is > 0
            ? maxOutputTokens.Value
            : DefaultMaxOutputTokens;

        var options = new ChatOptions
        {
            ModelId         = resolvedModel,
            MaxOutputTokens = tokens,
        };

        return (resolvedModel, messages, options);
    }

    /// <summary>解析模板 / 内联指令，合并 examples 与 outputFormat。</summary>
    private bool TryResolveInstruction(
        InstructChatRequestDto request,
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
        {
            error = (400, "either instruction or instructionId is required.");
            return false;
        }

        var examples = request.Examples ?? tpl?.Examples;
        var fmt      = !string.IsNullOrWhiteSpace(request.OutputFormat)
            ? request.OutputFormat
            : tpl?.OutputFormat;

        resolved = (instruction, examples, fmt);
        return true;
    }

    /// <summary>构造指令式 Chat 消息与 ChatOptions。</summary>
    private (string Model, List<ChatMessage> Messages, ChatOptions Options) BuildInstructInput(
        string instruction,
        IReadOnlyList<InstructExampleDto>? examples,
        string? outputFormat,
        InstructChatRequestDto request)
    {
        var model = ResolveModel(request.Model);

        var systemText = instruction.Trim();
        if (string.Equals(outputFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            systemText += "\n\n只输出合法 JSON，不要任何解释、代码块围栏或前后缀。";
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemText),
        };

        if (examples is { Count: > 0 })
        {
            foreach (var ex in examples)
            {
                if (string.IsNullOrWhiteSpace(ex.Input)) continue;
                messages.Add(new ChatMessage(ChatRole.User,      ex.Input));
                messages.Add(new ChatMessage(ChatRole.Assistant, ex.Output));
            }
        }

        messages.Add(new ChatMessage(ChatRole.User, AppendNoThink(request.Message)));

        var tokens = request.MaxOutputTokens is > 0
            ? request.MaxOutputTokens.Value
            : DefaultMaxOutputTokens;

        var options = new ChatOptions
        {
            ModelId         = model,
            MaxOutputTokens = tokens,
            Temperature     = request.Temperature,
            TopP            = request.TopP,
            Seed            = request.Seed,
            StopSequences   = request.Stop is { Count: > 0 }
                                ? request.Stop.ToList()
                                : null,
        };

        // ── 原生结构化输出：把 schema / json 模式转成 response_format ──
        if (request.UseStructuredOutput)
        {
            var rf = StructuredOutput.BuildResponseFormat(
                request.ResponseSchema, outputFormat);
            if (rf is not null)
            {
                if (options.AdditionalProperties is null)
                    options.AdditionalProperties = new();
                options.AdditionalProperties[StructuredOutput.AdditionalPropertiesKey] = rf;
            }
        }

        return (model, messages, options);
    }

    private static void PrepareNextTurn(
        List<ChatMessage> history,
        string systemPrompt,
        string previousAnswer,
        string? continuePrompt,
        bool keepHistory)
    {
        if (!keepHistory)
        {
            history.Clear();
            history.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }
        else
        {
            history.Add(new ChatMessage(ChatRole.Assistant, previousAnswer));
        }

        history.Add(new ChatMessage(ChatRole.User,
            BuildNextPrompt(continuePrompt, previousAnswer)));
    }

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
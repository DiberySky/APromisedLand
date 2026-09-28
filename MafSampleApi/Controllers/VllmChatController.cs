using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MafSampleApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MafSampleApi.Controllers;

/// <summary>
/// vLLM（Qwen3-4B-AWQ）专用聊天端点。
///
/// 路由：
///   POST /api/vllm/chat             —— 非流式
///   POST /api/vllm/chat/stream      —— 流式（SSE）
///   POST /api/vllm/chat/loop        —— 循环流式（SSE）
///   POST /api/vllm/chat/loop/sync   —— 循环非流式
/// </summary>
[ApiController]
[Route("api/vllm/chat")]
[Produces("application/json")]
public sealed class VllmChatController(
    IChatClient chatClient,
    IOptions<AgentOptions> agentOptions,
    ILogger<VllmChatController> logger) : ControllerBase
{
    private readonly AgentOptions _options = agentOptions.Value;

    private const string NoThinkHint = " /no_think";
    private const int DefaultMaxOutputTokens = 2048;
    private const int MaxRoundsLimit = 20;

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
                Model = model,
                Content = response.Text,
                Truncated = false,
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogInformation("vLLM chat client disconnected: model={Model}", model);
            return new EmptyResult();
        }
        catch (OperationCanceledException) when (budgetCts.IsCancellationRequested)
        {
            logger.LogWarning(
                "vLLM chat budget exhausted ({Budget}s): model={Model}",
                budgetSeconds, model);

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

        logger.LogInformation(
            "vLLM chat (stream): model={Model}, msgLen={Len}, maxTokens={Max}, budget={Budget}s",
            model, messages[^1].Text.Length, options.MaxOutputTokens, budgetSeconds);

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
            logger.LogInformation(
                "vLLM chat (stream) client disconnected: model={Model}", model);
            return;
        }
        catch (OperationCanceledException) when (budgetCts.IsCancellationRequested)
        {
            truncated = true;
            logger.LogWarning(
                "vLLM chat (stream) budget exhausted ({Budget}s): model={Model}",
                budgetSeconds, model);
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

        // 收尾帧（正常或截断）。用 ct 而非 chatCt —— budgetCts 已取消
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

        var model = ResolveModel(request.Model);
        var systemPrompt = ResolveSystemPrompt(request.SystemPrompt);
        var maxRounds = Math.Clamp(request.MaxRounds, 1, MaxRoundsLimit);
        var maxTokens = request.MaxOutputTokens is > 0
            ? request.MaxOutputTokens.Value
            : DefaultMaxOutputTokens;

        var chatOptions = new ChatOptions
        {
            ModelId = model,
            MaxOutputTokens = maxTokens,
        };

        var budgetSeconds = Math.Clamp(_options.VllmLoopBudgetSeconds, 10, 900);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var loopCt = budgetCts.Token;

        var history = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, AppendNoThink(request.Message)),
        };

        logger.LogInformation(
            "vLLM loop (stream) start: model={Model}, maxRounds={MaxRounds}, " +
            "keepHistory={Keep}, budget={Budget}s",
            model, maxRounds, request.KeepHistory, budgetSeconds);

        await WriteEventAsync(new VllmLoopStreamChunkDto
        {
            Model = model, Round = 0, MaxRounds = maxRounds,
            Phase = "start", Done = false,
        }, ct);

        var finished = 0;
        var truncated = false;

        for (var round = 1; round <= maxRounds && !ct.IsCancellationRequested; round++)
        {
            if (loopCt.IsCancellationRequested)
            {
                truncated = true;
                logger.LogWarning(
                    "vLLM loop (stream) budget exhausted before round {Round}",
                    round);
                break;
            }

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
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException) when (budgetCts.IsCancellationRequested)
            {
                truncated = true;
                logger.LogWarning(
                    "vLLM loop (stream) budget exhausted at round {Round}", round);
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "vLLM loop (stream) failed: model={Model}, round={Round}",
                    model, round);

                await WriteEventAsync(new VllmLoopStreamChunkDto
                {
                    Model = model, Round = round, MaxRounds = maxRounds,
                    Content = $"[error] {ex.Message}",
                    Phase = "error", Done = true,
                }, ct);
                return;
            }

            // 若本轮因预算中断：不发 round_end，直接收尾
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

        var model = ResolveModel(request.Model);
        var systemPrompt = ResolveSystemPrompt(request.SystemPrompt);
        var maxRounds = Math.Clamp(request.MaxRounds, 1, MaxRoundsLimit);
        var maxTokens = request.MaxOutputTokens is > 0
            ? request.MaxOutputTokens.Value
            : DefaultMaxOutputTokens;

        var chatOptions = new ChatOptions
        {
            ModelId = model,
            MaxOutputTokens = maxTokens,
        };

        var budgetSeconds = Math.Clamp(_options.VllmLoopBudgetSeconds, 10, 900);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var loopCt = budgetCts.Token;

        var history = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, AppendNoThink(request.Message)),
        };

        logger.LogInformation(
            "vLLM loop (sync) start: model={Model}, maxRounds={MaxRounds}, " +
            "keepHistory={Keep}, budget={Budget}s",
            model, maxRounds, request.KeepHistory, budgetSeconds);

        var rounds = new List<VllmLoopRoundDto>(maxRounds);
        var truncated = false;

        for (var round = 1; round <= maxRounds; round++)
        {
            if (ct.IsCancellationRequested)
                return new EmptyResult();

            if (loopCt.IsCancellationRequested)
            {
                truncated = true;
                logger.LogWarning(
                    "vLLM loop (sync) budget exhausted before round {Round}", round);
                break;
            }

            try
            {
                var response = await chatClient.GetResponseAsync(history, chatOptions, loopCt);
                var text = response.Text;

                rounds.Add(new VllmLoopRoundDto
                {
                    Round = round,
                    Content = text,
                });

                logger.LogInformation(
                    "vLLM loop (sync) round {Round}/{MaxRounds} done: len={Len}",
                    round, maxRounds, text.Length);

                if (round == maxRounds) break;

                PrepareNextTurn(history, systemPrompt, text,
                    request.ContinuePrompt, request.KeepHistory);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.LogInformation(
                    "vLLM loop (sync) client disconnected: round={Round}", round);
                return new EmptyResult();
            }
            catch (OperationCanceledException) when (budgetCts.IsCancellationRequested)
            {
                truncated = true;
                logger.LogWarning(
                    "vLLM loop (sync) budget exhausted at round {Round}", round);
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "vLLM loop (sync) failed: model={Model}, round={Round}",
                    model, round);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = ex.Message, model, round });
            }
        }

        return Ok(new VllmLoopResponseDto
        {
            Model = model,
            MaxRounds = maxRounds,
            CompletedRounds = rounds.Count,
            Truncated = truncated,
            Rounds = rounds,
            Content = rounds.Count > 0 ? rounds[^1].Content : string.Empty,
        });
    }

    // ═══════════════════════════════════════════════════════════
//  AI 指令 Chat —— 非流式
// ═══════════════════════════════════════════════════════════
    [HttpPost("instruct")]
    public async Task<ActionResult<InstructChatResponseDto>> InstructAsync(
        [FromBody] InstructChatRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Instruction))
            return BadRequest(new { error = "instruction is required." });
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message is required." });

        var (model, messages, options) = BuildInstructInput(request);

        var budgetSeconds = Math.Clamp(_options.VllmChatBudgetSeconds, 5, 600);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var chatCt = budgetCts.Token;

        logger.LogInformation(
            "vLLM instruct: model={Model}, msgLen={Len}, examples={Ex}, fmt={Fmt}, budget={B}s",
            model, request.Message.Length, request.Examples?.Count ?? 0,
            request.OutputFormat ?? "text", budgetSeconds);

        try
        {
            var response = await chatClient.GetResponseAsync(messages, options, chatCt);
            return Ok(new InstructChatResponseDto
            {
                Model = model,
                Content = response.Text,
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
//  AI 指令 Chat —— 流式 SSE
// ═══════════════════════════════════════════════════════════
    [HttpPost("instruct/stream")]
    public async Task InstructStreamAsync(
        [FromBody] InstructChatRequestDto request,
        CancellationToken ct)
    {
        SetSseHeaders();

        if (string.IsNullOrWhiteSpace(request.Instruction))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteEventAsync(new { error = "instruction is required." }, ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteEventAsync(new { error = "message is required." }, ct);
            return;
        }

        var (model, messages, options) = BuildInstructInput(request);

        var budgetSeconds = Math.Clamp(_options.VllmChatBudgetSeconds, 5, 600);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var chatCt = budgetCts.Token;

        logger.LogInformation(
            "vLLM instruct (stream): model={Model}, examples={Ex}, fmt={Fmt}, budget={B}s",
            model, request.Examples?.Count ?? 0,
            request.OutputFormat ?? "text", budgetSeconds);

        await WriteEventAsync(new InstructStreamChunkDto
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
            logger.LogWarning(
                "vLLM instruct (stream) budget exhausted ({B}s): model={Model}",
                budgetSeconds, model);
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

        await WriteEventAsync(new InstructStreamChunkDto
        {
            Model = model, Content = "", Done = true, Truncated = truncated,
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
        var resolvedModel = ResolveModel(model);
        var resolvedSystem = ResolveSystemPrompt(systemPrompt);
        var userMessage = AppendNoThink(message);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, resolvedSystem),
            new(ChatRole.User, userMessage),
        };

        var tokens = maxOutputTokens is > 0
            ? maxOutputTokens.Value
            : DefaultMaxOutputTokens;

        var options = new ChatOptions
        {
            ModelId = resolvedModel,
            MaxOutputTokens = tokens,
        };

        return (resolvedModel, messages, options);
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
    
    /// <summary>构造指令式 Chat 的消息序列与 ChatOptions。</summary>
    private (string Model, List<ChatMessage> Messages, ChatOptions Options) BuildInstructInput(
        InstructChatRequestDto request)
    {
        var model = ResolveModel(request.Model);

        // system = instruction (+ JSON 输出规范)
        var systemText = request.Instruction.Trim();
        if (string.Equals(request.OutputFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            systemText += "\n\n只输出合法 JSON，不要任何解释、代码块围栏或前后缀。";
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemText),
        };

        // few-shot: user/assistant 交替
        if (request.Examples is { Count: > 0 })
        {
            foreach (var ex in request.Examples)
            {
                if (string.IsNullOrWhiteSpace(ex.Input)) continue;
                messages.Add(new ChatMessage(ChatRole.User,      ex.Input));
                messages.Add(new ChatMessage(ChatRole.Assistant, ex.Output));
            }
        }

        // 用户实际输入
        messages.Add(new ChatMessage(ChatRole.User, AppendNoThink(request.Message)));

        var tokens = request.MaxOutputTokens is > 0
            ? request.MaxOutputTokens.Value
            : DefaultMaxOutputTokens;

        var options = new ChatOptions
        {
            ModelId         = model,
            MaxOutputTokens = tokens,
            Temperature     = request.Temperature,   // null = 用服务端默认
        };

        return (model, messages, options);
    }
}
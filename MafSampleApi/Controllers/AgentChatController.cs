using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MafSampleApi.Models;
using MafSampleApi.Services;
using MafSampleApi.Services.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Agents.AI;

// ★ 关键：解决 AgentSessionStore 命名冲突
using AgentSessionStore = MafSampleApi.Services.AgentSessionStore;

namespace MafSampleApi.Controllers;

/// <summary>
/// MAF Agent + vLLM 增强版 Chat（方案 C：基础工具 + 高级工具按需）。
/// </summary>
[ApiController]
[Route("api/agent/chat")]
[Produces("application/json")]
public sealed class AgentChatController(
    AgentSessionStore sessionStore,
    IInstructionTemplateStore templates,
    IToolRegistry toolRegistry,
    IOptions<AgentOptions> agentOptions,
    ILogger<AgentChatController> logger) : ControllerBase
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
            return resolveError!.Value.status == 404
                ? NotFound(new { error = resolveError.Value.msg })
                : BadRequest(new { error = resolveError.Value.msg });

        var sessionId = ResolveSessionId(request.SessionId);
        var model     = ResolveModel(request.Model);
        var sysText   = BuildSystemText(resolved.instruction, resolved.outputFormat);
        var chatOpts  = BuildChatOptions(model, request);

        var baseTools    = toolRegistry.ResolveBase();
        var dynamicTools = ResolveDynamicTools(request);
        var allTools     = MergeTools(baseTools, dynamicTools);

        var entry = await sessionStore.GetOrCreateAsync(
            sessionId, model, sysText, chatOpts, baseTools, ct);

        var budgetSeconds = Math.Clamp(_options.ChatBudgetSeconds, 15, 1200);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var chatCt = budgetCts.Token;

        var maxAttempts = 1 + Math.Clamp(request.MaxRetries, 0, MaxSchemaRetriesLimit);
        var needSchema  = !string.IsNullOrWhiteSpace(request.ResponseSchema);

        logger.LogInformation(
            "Agent chat: session={Sid}, model={M}, baseTools={B}, dynTools={D}, schema={Sc}, budget={Bu}s",
            sessionId, model, baseTools.Count, dynamicTools.Count,
            needSchema ? "yes" : "no", budgetSeconds);

        try
        {
            string userMsg = AppendNoThink(request.Message);
            string? schemaErr = null;
            string content = string.Empty;
            int attempt = 0;
            ChatResponse? lastResp = null;

            for (; attempt < maxAttempts; attempt++)
            {
                lastResp = await RunOnceAsync(entry, userMsg, allTools, chatCt);

                content = lastResp.Text;

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

                if (attempt < maxAttempts - 1)
                {
                    userMsg =
                        $"你上一次的输出不符合 JSON Schema。错误：{err}。" +
                        "请严格按 Schema 重新输出，只输出 JSON。";
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
                ToolCalls   = ExtractToolCalls(lastResp?.Messages),
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
            logger.LogError(ex, "Agent chat failed: session={Sid}", sessionId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = BuildErrorDetail(ex), sessionId, model });
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

        var baseTools    = toolRegistry.ResolveBase();
        var dynamicTools = ResolveDynamicTools(request);
        var allTools     = MergeTools(baseTools, dynamicTools);

        var entry = await sessionStore.GetOrCreateAsync(
            sessionId, model, sysText, chatOpts, baseTools, ct);

        var budgetSeconds = Math.Clamp(_options.ChatBudgetSeconds, 15, 1200);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var chatCt = budgetCts.Token;

        await WriteEventAsync(new AgentStreamChunkDto
        {
            SessionId = sessionId, Model = model, Content = "", Done = false
        }, ct);

        var sb        = new StringBuilder();
        var truncated = false;

        try
        {
            var userMsg = AppendNoThink(request.Message);
            var runOptions = BuildRunOptions(entry, allTools);

            await foreach (var update in entry.Agent.RunStreamingAsync(
                               userMsg,
                               session: entry.Session,
                               options: runOptions,
                               cancellationToken: chatCt))
            {
                if (chatCt.IsCancellationRequested) break;

                foreach (var content in update.Contents)
                {
                    switch (content)
                    {
                        case FunctionCallContent fc:
                            await WriteEventAsync(new AgentStreamChunkDto
                            {
                                SessionId = sessionId, Model = model,
                                Phase = "tool_call",
                                ToolCallId = fc.CallId,
                                ToolName = fc.Name,
                                ToolArgumentsJson = fc.Arguments is null ? "{}"
                                    : JsonSerializer.Serialize(fc.Arguments),
                                Done = false,
                            }, ct);
                            continue;

                        case FunctionResultContent fr:
                            await WriteEventAsync(new AgentStreamChunkDto
                            {
                                SessionId = sessionId, Model = model,
                                Phase = "tool_result",
                                ToolCallId = fr.CallId,
                                ToolResultText = fr.Result?.ToString(),
                                Done = false,
                            }, ct);
                            continue;

                        case TextContent tc when !string.IsNullOrEmpty(tc.Text):
                            sb.Append(tc.Text);
                            await WriteEventAsync(new AgentStreamChunkDto
                            {
                                SessionId = sessionId, Model = model,
                                Content = tc.Text, Phase = "delta", Done = false,
                            }, ct);
                            continue;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        catch (OperationCanceledException) when (budgetCts.IsCancellationRequested)
        {
            truncated = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent chat (stream) failed: session={Sid}", sessionId);
            await WriteEventAsync(new AgentStreamChunkDto
            {
                SessionId = sessionId, Model = model,
                Content = $"[error] {ex.Message}", Phase = "delta", Done = true,
            }, ct);
            return;
        }

        var schemaValid = true;
        string? schemaErr = null;
        if (!truncated && !string.IsNullOrWhiteSpace(request.ResponseSchema))
            schemaValid = JsonSchemaValidator.TryValidate(
                sb.ToString(), request.ResponseSchema, out schemaErr);

        await WriteEventAsync(new AgentStreamChunkDto
        {
            SessionId = sessionId, Model = model,
            Content = "", Done = true,
            Truncated = truncated, SchemaValid = schemaValid, SchemaError = schemaErr,
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════
    //  循环（流式）
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

        var baseTools    = toolRegistry.ResolveBase();
        var dynamicTools = ResolveDynamicTools(request);
        var allTools     = MergeTools(baseTools, dynamicTools);

        var entry = await sessionStore.GetOrCreateAsync(
            sessionId, model, sysText, chatOpts, baseTools, ct);

        var budgetSeconds = Math.Clamp(_options.LoopBudgetSeconds, 30, 1200);
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budgetCts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        var loopCt = budgetCts.Token;

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
                Round = round, MaxRounds = maxRounds, Phase = "round_start", Done = false,
            }, ct);

            try
            {
                var runOptions = BuildRunOptions(entry, allTools);

                await foreach (var update in entry.Agent.RunStreamingAsync(
                                   prompt,
                                   session: entry.Session,
                                   options: runOptions,
                                   cancellationToken: loopCt))
                {
                    if (loopCt.IsCancellationRequested) break;

                    foreach (var content in update.Contents)
                    {
                        switch (content)
                        {
                            case FunctionCallContent fc:
                                await WriteEventAsync(new LoopStreamChunkDto
                                {
                                    SessionId = sessionId, Model = model,
                                    Round = round, MaxRounds = maxRounds,
                                    Phase = "tool_call",
                                    ToolCallId = fc.CallId,
                                    ToolName = fc.Name,
                                    ToolArgumentsJson = fc.Arguments is null ? "{}"
                                        : JsonSerializer.Serialize(fc.Arguments),
                                    Done = false,
                                }, ct);
                                continue;

                            case FunctionResultContent fr:
                                await WriteEventAsync(new LoopStreamChunkDto
                                {
                                    SessionId = sessionId, Model = model,
                                    Round = round, MaxRounds = maxRounds,
                                    Phase = "tool_result",
                                    ToolCallId = fr.CallId,
                                    ToolResultText = fr.Result?.ToString(),
                                    Done = false,
                                }, ct);
                                continue;

                            case TextContent tc when !string.IsNullOrEmpty(tc.Text):
                                sb.Append(tc.Text);
                                await WriteEventAsync(new LoopStreamChunkDto
                                {
                                    SessionId = sessionId, Model = model,
                                    Round = round, MaxRounds = maxRounds,
                                    Content = tc.Text, Phase = "delta", Done = false,
                                }, ct);
                                continue;
                        }
                    }
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
                    Content = $"[error] {ex.Message}", Phase = "error", Done = true,
                }, ct);
                return;
            }

            if (truncated) break;

            var text = sb.ToString();
            finished = round;

            await WriteEventAsync(new LoopStreamChunkDto
            {
                SessionId = sessionId, Model = model,
                Round = round, MaxRounds = maxRounds, Phase = "round_end", Done = false,
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
    //  循环（非流式）
    // ═══════════════════════════════════════════════════════════
    [HttpPost("loop/sync")]
    public async Task<ActionResult<LoopChatResponseDto>> LoopSyncAsync(
        [FromBody] AgentLoopRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message is required." });

        if (!TryResolveInstruction(request, out var resolved, out var resolveError))
            return resolveError!.Value.status == 404
                ? NotFound(new { error = resolveError.Value.msg })
                : BadRequest(new { error = resolveError.Value.msg });

        var sessionId = ResolveSessionId(request.SessionId);
        var model     = ResolveModel(request.Model);
        var sysText   = BuildSystemText(resolved.instruction, resolved.outputFormat);
        var chatOpts  = BuildChatOptions(model, request);
        var maxRounds = Math.Clamp(request.MaxRounds, 1, MaxRoundsLimit);

        var baseTools    = toolRegistry.ResolveBase();
        var dynamicTools = ResolveDynamicTools(request);
        var allTools     = MergeTools(baseTools, dynamicTools);

        var entry = await sessionStore.GetOrCreateAsync(
            sessionId, model, sysText, chatOpts, baseTools, ct);

        var budgetSeconds = Math.Clamp(_options.LoopBudgetSeconds, 30, 1200);
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
                var resp = await RunOnceAsync(entry, prompt, allTools, loopCt);
                var text = resp.Text;
                var toolCalls = ExtractToolCalls(resp.Messages);
                rounds.Add(new LoopRoundDto
                {
                    Round = round,
                    Content = text,
                    ToolCalls = toolCalls,
                });

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
    //  Run 封装
    // ═══════════════════════════════════════════════════════════

    /// <summary>单次非流式调用，返回 AgentRunResponse 原始类型。</summary>
    private static async Task<ChatResponse> RunOnceAsync(
        AgentSessionStore.Entry entry,
        string userMessage,
        IReadOnlyList<AIFunction> allTools,
        CancellationToken ct)
    {
        var runOptions = BuildRunOptions(entry, allTools);

        var resp = await entry.Agent.RunAsync(
            userMessage,
            session: entry.Session,
            options: runOptions,
            cancellationToken: ct);

        // 从 Agent 的响应里取消息列表，包成 ChatResponse
        return new ChatResponse(resp.Messages.ToList());
    }

    /// <summary>
    /// 构造 RunOptions。注意：RunOptions 里的 Tools 是**覆盖**语义，
    /// 所以要把基础 + 动态工具合并后一起传，并把 Instructions / ModelId 补上。
    /// ★ 修复：必须同时把 Agent 创建时绑定的采样参数（Temperature/TopP/Seed/Stop/MaxOutputTokens）
    ///   一起复制过来，否则带工具的请求会回退到模型默认值，丢失用户传入的采样配置。
    /// </summary>
    private static ChatClientAgentRunOptions? BuildRunOptions(
        AgentSessionStore.Entry entry,
        IReadOnlyList<AIFunction> allTools)
    {
        if (allTools.Count == 0) return null;

        var src = entry.BoundChatOptions;
        return new ChatClientAgentRunOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions    = entry.SystemPrompt,
                ModelId         = entry.Model,
                Temperature     = src.Temperature,
                TopP            = src.TopP,
                Seed            = src.Seed,
                StopSequences   = src.StopSequences,
                MaxOutputTokens = src.MaxOutputTokens,
                // ★ ChatOptions.Tools 类型是 IList<AITool>?，需要 Cast
                Tools           = allTools.Cast<AITool>().ToList(),
            },
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  工具解析
    // ═══════════════════════════════════════════════════════════

    // private IReadOnlyList<AIFunction> ResolveDynamicTools(AgentChatRequestDto r)
    //     => r.DisableDynamicTools
    //         ? Array.Empty<AIFunction>()
    //         : toolRegistry.ResolveDynamic(r.Tools, r.ToolTags);
    
    private IReadOnlyList<AIFunction> ResolveDynamicTools(AgentChatRequestDto r)
    {
        var names = r.Tools is { Count: > 0 } ? string.Join(",", r.Tools) : "(default)";
        logger.LogInformation(
            "ResolveDynamicTools: tools={Names}, tags={Tags}, disable={Disable}",
            names,
            r.ToolTags is { Count: > 0 } ? string.Join(",", r.ToolTags) : "-",
            r.DisableDynamicTools);

        return r.DisableDynamicTools
            ? Array.Empty<AIFunction>()
            : toolRegistry.ResolveDynamic(r.Tools, r.ToolTags);
    }

    private IReadOnlyList<AIFunction> ResolveDynamicTools(AgentLoopRequestDto r)
        => r.DisableDynamicTools
            ? Array.Empty<AIFunction>()
            : toolRegistry.ResolveDynamic(r.Tools, r.ToolTags);

    private static IReadOnlyList<AIFunction> MergeTools(
        IReadOnlyList<AIFunction> baseTools,
        IReadOnlyList<AIFunction> dynamicTools)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<AIFunction>(baseTools.Count + dynamicTools.Count);

        foreach (var f in baseTools)
            if (seen.Add(f.Name)) result.Add(f);

        foreach (var f in dynamicTools)
            if (seen.Add(f.Name)) result.Add(f);

        return result;
    }

    private static IReadOnlyList<AgentToolCallDto>? ExtractToolCalls(
        IEnumerable<ChatMessage>? messages)
    {
        if (messages is null) return null;

        var calls   = new Dictionary<string, AgentToolCallDto>(StringComparer.Ordinal);
        var results = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var msg in messages)
        foreach (var c in msg.Contents)
        {
            switch (c)
            {
                case FunctionCallContent fc:
                    calls[fc.CallId] = new AgentToolCallDto
                    {
                        CallId        = fc.CallId,
                        Name          = fc.Name,
                        ArgumentsJson = fc.Arguments is null ? "{}"
                            : JsonSerializer.Serialize(fc.Arguments),
                    };
                    break;

                case FunctionResultContent fr:
                    results[fr.CallId] = fr.Result?.ToString() ?? string.Empty;
                    break;
            }
        }

        if (calls.Count == 0) return null;

        return calls.Values.Select(c => c with
        {
            ResultText = results.TryGetValue(c.CallId, out var r) ? r : null,
        }).ToArray();
    }

    // ═══════════════════════════════════════════════════════════
    //  通用工具方法
    // ═══════════════════════════════════════════════════════════

    private static string ResolveSessionId(string? provided)
        => string.IsNullOrWhiteSpace(provided)
            ? Guid.NewGuid().ToString("N")
            : provided.Trim();

    private string ResolveModel(string? requested)
        => string.IsNullOrWhiteSpace(requested) ? _options.ChatModel : requested;

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

    private bool TryResolveInstruction(
        AgentLoopRequestDto request,
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

        var fmt = !string.IsNullOrWhiteSpace(request.OutputFormat)
            ? request.OutputFormat
            : tpl?.OutputFormat;

        resolved = (instruction, request.Examples ?? tpl?.Examples, fmt);
        return true;
    }

    private static string BuildSystemText(string instruction, string? outputFormat)
    {
        var text = instruction.Trim();
        if (string.Equals(outputFormat, "json", StringComparison.OrdinalIgnoreCase))
            text += "\n\n只输出合法 JSON，不要任何解释、代码块围栏或前后缀。";
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
    
    /// <summary>把 ClientResultException 里的 response body 提取出来，方便诊断。</summary>
    private static string BuildErrorDetail(Exception ex)
    {
        var msg = ex.Message;

        // OpenAI SDK 的 ClientResultException 里藏了 HTTP body
        if (ex is System.ClientModel.ClientResultException cre)
        {
            try
            {
                var raw  = cre.GetRawResponse();
                var body = raw?.Content?.ToString();
                if (!string.IsNullOrWhiteSpace(body))
                    msg += "\n--- vLLM response body ---\n" + body;
            }
            catch { /* 拿不到就算了 */ }
        }

        return msg;
    }
}
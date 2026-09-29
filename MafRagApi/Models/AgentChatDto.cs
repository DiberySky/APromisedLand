namespace MafRagApi.Models;

/// <summary>POST /api/agent/chat 系列请求体。</summary>
public sealed record AgentChatRequestDto
{
    /// <summary>用户输入（必填）。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>会话 ID；不传则服务端生成。</summary>
    public string? SessionId { get; init; }

    // ─── 指令来源 ──────────────────────────────
    /// <summary>引用服务端预置模板。</summary>
    public string? InstructionId { get; init; }

    /// <summary>内联 system prompt；与 InstructionId 同时给时本字段优先。</summary>
    public string? Instruction { get; init; }

    /// <summary>few-shot 示例，未给则用模板里的。</summary>
    public IReadOnlyList<InstructExampleDto>? Examples { get; init; }

    /// <summary>"text"（默认）或 "json"。</summary>
    public string? OutputFormat { get; init; }

    // ─── 采样参数 ──────────────────────────────
    public float? Temperature { get; init; }
    public float? TopP        { get; init; }
    public long?  Seed        { get; init; }
    public IReadOnlyList<string>? Stop { get; init; }

    // ─── Schema 校验 ──────────────────────────
    public string? ResponseSchema { get; init; }

    /// <summary>
    /// 是否启用 vLLM 原生结构化输出（response_format）。
    /// true（默认）：传 ResponseSchema 时走 json_schema 严格模式，仅 OutputFormat=json 时走 json_object。
    /// false：只做后置校验 + 重试，不向 vLLM 传 response_format。
    /// </summary>
    public bool UseStructuredOutput { get; init; } = true;

    public int MaxRetries { get; init; } = 1;

    // ─── 模型 ──────────────────────────────────
    public string? Model           { get; init; }
    public int?    MaxOutputTokens { get; init; }
    
    // ─── 工具调用 ──────────────────────────────
    /// <summary>动态启用的高级工具名单。基础工具自动随 Agent 加载，无需列出。</summary>
    public IReadOnlyList<string>? Tools { get; init; }

    /// <summary>按标签启用（与 Tools 二选一；同时给时 Tools 优先）。</summary>
    public IReadOnlyList<string>? ToolTags { get; init; }

    /// <summary>禁用所有高级工具（基础工具不受影响）。</summary>
    public bool DisableDynamicTools { get; init; }
}

/// <summary>POST /api/agent/chat 响应体。</summary>
public sealed record AgentChatResponseDto
{
    public string  SessionId   { get; init; } = string.Empty;
    public string  Model       { get; init; } = string.Empty;
    public string  Content     { get; init; } = string.Empty;
    public bool    Truncated   { get; init; }
    public int     Attempts    { get; init; } = 1;
    public bool    SchemaValid { get; init; } = true;
    public string? SchemaError { get; init; }
    public IReadOnlyList<AgentToolCallDto>? ToolCalls { get; init; }
}

/// <summary>POST /api/agent/chat/stream 的 SSE 分片。</summary>
public sealed record AgentStreamChunkDto
{
    public string  SessionId   { get; init; } = string.Empty;
    public string  Model       { get; init; } = string.Empty;
    public string  Content     { get; init; } = string.Empty;
    public bool    Done        { get; init; }
    public bool    Truncated   { get; init; }
    public bool    SchemaValid { get; init; } = true;
    public string? SchemaError { get; init; }
    
    /// <summary>"delta"（默认）| "tool_call" | "tool_result"</summary>
    public string Phase { get; init; } = "delta";

    /// <summary>仅 Phase=tool_call 时有值。</summary>
    public string? ToolName { get; init; }
    public string? ToolCallId { get; init; }
    public string? ToolArgumentsJson { get; init; }

    /// <summary>仅 Phase=tool_result 时有值。</summary>
    public string? ToolResultText { get; init; }
}

/// <summary>POST /api/agent/chat/loop 与 /loop/sync 请求体。</summary>
public sealed record AgentLoopRequestDto
{
    public string Message { get; init; } = string.Empty;
    public string? SessionId { get; init; }
    public string? InstructionId { get; init; }
    public string? Instruction { get; init; }
    public IReadOnlyList<InstructExampleDto>? Examples { get; init; }
    public string? OutputFormat { get; init; }

    public float? Temperature { get; init; }
    public float? TopP        { get; init; }
    public long?  Seed        { get; init; }
    public IReadOnlyList<string>? Stop { get; init; }

    public string? ContinuePrompt { get; init; }
    public int MaxRounds { get; init; } = 3;

    public string? Model           { get; init; }
    public int?    MaxOutputTokens { get; init; }
    
    // ─── 工具调用 ──────────────────────────────
    /// <summary>动态启用的高级工具名单。基础工具自动随 Agent 加载。</summary>
    public IReadOnlyList<string>? Tools { get; init; }

    /// <summary>按标签启用（与 Tools 二选一；同时给时 Tools 优先）。</summary>
    public IReadOnlyList<string>? ToolTags { get; init; }

    /// <summary>禁用所有高级工具（基础工具不受影响）。</summary>
    public bool DisableDynamicTools { get; init; }
}

public sealed record AgentToolCallDto
{
    public string CallId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ArgumentsJson { get; init; } = string.Empty;
    public string? ResultText { get; init; }
}

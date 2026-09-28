namespace MafSampleApi.Models;

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
    public int MaxRetries { get; init; } = 1;

    // ─── 模型 ──────────────────────────────────
    public string? Model           { get; init; }
    public int?    MaxOutputTokens { get; init; }
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
}
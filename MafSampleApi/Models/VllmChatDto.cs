namespace MafSampleApi.Models;

public sealed record VllmChatRequestDto
{
    public string Message { get; init; } = string.Empty;
    public string? Model { get; init; }
    public string? SystemPrompt { get; init; }
    public int? MaxOutputTokens { get; init; }
}

public sealed record VllmChatResponseDto
{
    public string Model { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    /// <summary>true 表示被服务端时间预算截断。</summary>
    public bool Truncated { get; init; }
}

public sealed record VllmStreamChunkDto
{
    public string Model { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public bool Done { get; init; }
    /// <summary>仅 Done=true 的收尾帧有意义。</summary>
    public bool Truncated { get; init; }
}

// ─── 循环 ──────────────────────────────────────────────

public sealed record VllmLoopRequestDto
{
    public string Message { get; init; } = string.Empty;
    public string? Model { get; init; }
    public string? SystemPrompt { get; init; }
    public int? MaxOutputTokens { get; init; }
    public string? ContinuePrompt { get; init; }
    public int MaxRounds { get; init; } = 3;
    public bool KeepHistory { get; init; } = true;
}

public sealed record VllmLoopRoundDto
{
    public int Round { get; init; }
    public string Content { get; init; } = string.Empty;
}

public sealed record VllmLoopResponseDto
{
    public string Model { get; init; } = string.Empty;
    public int MaxRounds { get; init; }
    public int CompletedRounds { get; init; }
    /// <summary>true 表示时间预算耗尽，未跑满 MaxRounds。</summary>
    public bool Truncated { get; init; }
    public IReadOnlyList<VllmLoopRoundDto> Rounds { get; init; } = Array.Empty<VllmLoopRoundDto>();
    public string Content { get; init; } = string.Empty;
}

public sealed record VllmLoopStreamChunkDto
{
    public string Model { get; init; } = string.Empty;
    public int Round { get; init; }
    public int MaxRounds { get; init; }
    public string Content { get; init; } = string.Empty;
    /// <summary>start | round_start | delta | round_end | done | error</summary>
    public string Phase { get; init; } = "delta";
    public bool Done { get; init; }
    /// <summary>仅 Phase=done 的收尾帧有意义。</summary>
    public bool Truncated { get; init; }
}

/// ─── AI 指令 Chat ──────────────────────────────────────────

public sealed record InstructExampleDto
{
    public string Input  { get; init; } = string.Empty;
    public string Output { get; init; } = string.Empty;
}

/// <summary>POST /api/vllm/chat/instruct 与 /instruct/stream 的请求体。</summary>
public sealed record InstructChatRequestDto
{
    // ─── 指令来源 ───────────────────────────────
    /// <summary>引用服务端预置模板；与 Instruction 同时给时，Instruction 覆盖模板。</summary>
    public string? InstructionId { get; init; }

    /// <summary>内联指令。与 InstructionId 至少提供一个。</summary>
    public string? Instruction { get; init; }

    /// <summary>用户输入（必填）。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>可选 few-shot 示例。未给则用模板里的。</summary>
    public IReadOnlyList<InstructExampleDto>? Examples { get; init; }

    /// <summary>"text"（默认）或 "json"。json 时自动追加输出规范提示。</summary>
    public string? OutputFormat { get; init; }

    // ─── 采样参数 ───────────────────────────────
    public float?  Temperature { get; init; }
    public float?  TopP        { get; init; }
    public long?   Seed        { get; init; }
    /// <summary>遇到任一字符串即停止生成。</summary>
    public IReadOnlyList<string>? Stop { get; init; }

    // ─── 结构化输出校验 ─────────────────────────
    /// <summary>JSON Schema 字符串。提供时服务端会校验输出；不合规触发重试。</summary>
    public string? ResponseSchema { get; init; }

    /// <summary>schema 校验失败时的最大重试次数（默认 1，即最多发 2 次请求）。</summary>
    public int MaxRetries { get; init; } = 1;

    // ─── 模型 ──────────────────────────────────
    public string? Model           { get; init; }
    public int?    MaxOutputTokens { get; init; }
}

/// <summary>POST /api/vllm/chat/instruct 的响应体。</summary>
public sealed record InstructChatResponseDto
{
    public string  Model       { get; init; } = string.Empty;
    public string  Content     { get; init; } = string.Empty;
    public bool    Truncated   { get; init; }
    /// <summary>实际调用次数（含重试）。</summary>
    public int     Attempts    { get; init; } = 1;
    /// <summary>未提供 schema 时为 true。</summary>
    public bool    SchemaValid { get; init; } = true;
    /// <summary>schema 校验错误摘要（仅 SchemaValid=false 时有值）。</summary>
    public string? SchemaError { get; init; }
}

/// <summary>POST /api/vllm/chat/instruct/stream 的 SSE 分片。</summary>
public sealed record InstructStreamChunkDto
{
    public string  Model       { get; init; } = string.Empty;
    public string  Content     { get; init; } = string.Empty;
    public bool    Done        { get; init; }
    public bool    Truncated   { get; init; }
    /// <summary>仅在 Done=true 的收尾帧有意义。</summary>
    public bool    SchemaValid { get; init; } = true;
    /// <summary>仅在 Done=true 的收尾帧有意义。</summary>
    public string? SchemaError { get; init; }
}



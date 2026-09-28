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

// ─── AI 指令 Chat ──────────────────────────────────────────

/// <summary>few-shot 示例：一问一答。</summary>
public sealed record InstructExampleDto
{
    public string Input  { get; init; } = string.Empty;
    public string Output { get; init; } = string.Empty;
}

/// <summary>POST /api/vllm/chat/instruct 的请求体。</summary>
public sealed record InstructChatRequestDto
{
    /// <summary>指令：角色、任务、输出规范等（必填）。</summary>
    public string Instruction { get; init; } = string.Empty;

    /// <summary>用户输入（必填）。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>可选 few-shot 示例，按顺序作为 user/assistant 交替消息注入。</summary>
    public IReadOnlyList<InstructExampleDto>? Examples { get; init; }

    /// <summary>"text"（默认）或 "json"。json 时自动追加输出规范提示。</summary>
    public string? OutputFormat { get; init; }

    /// <summary>0.0~2.0，不传则用 vLLM 默认。</summary>
    public float? Temperature { get; init; }

    public string? Model { get; init; }
    public int? MaxOutputTokens { get; init; }
}

/// <summary>POST /api/vllm/chat/instruct 的响应体。</summary>
public sealed record InstructChatResponseDto
{
    public string Model       { get; init; } = string.Empty;
    public string Content     { get; init; } = string.Empty;
    public bool   Truncated   { get; init; }
}

/// <summary>流式分片。</summary>
public sealed record InstructStreamChunkDto
{
    public string Model     { get; init; } = string.Empty;
    public string Content   { get; init; } = string.Empty;
    public bool   Done      { get; init; }
    public bool   Truncated { get; init; }
}
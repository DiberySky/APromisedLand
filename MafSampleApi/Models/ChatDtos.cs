namespace MafSampleApi.Models;

/// <summary>POST /api/chat 与 /api/chat/stream 的请求体。</summary>
public sealed record ChatRequestDto
{
    public string Message { get; init; } = string.Empty;

    /// <summary>可选；不传则服务端生成一个新的 GUID。</summary>
    public string? SessionId { get; init; }

    /// <summary>可选；不传则用 AgentOptions.ChatModel。</summary>
    public string? Model { get; init; }

    // ─── 结构化输出 ──────────────────────────────
    /// <summary>"text"（默认）或 "json"。json 时走 vLLM json_object 模式。</summary>
    public string? OutputFormat { get; init; }

    /// <summary>JSON Schema 字符串。提供时走 vLLM json_schema 严格模式。</summary>
    public string? ResponseSchema { get; init; }

    /// <summary>
    /// 是否启用 vLLM 原生结构化输出（response_format），默认 true。
    /// false 时只输出自由文本，不向 vLLM 传 response_format。
    /// </summary>
    public bool UseStructuredOutput { get; init; } = true;
}

/// <summary>非流式响应。</summary>
public sealed record ChatResponseDto
{
    public string SessionId { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

/// <summary>SSE 流式分片。</summary>
public sealed record StreamChunkDto
{
    public string SessionId { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public bool Done { get; init; }
}

/// <summary>健康检查响应。</summary>
public sealed record HealthResponseDto
{
    public string Status { get; init; } = "ok";
    public string ChatModel { get; init; } = string.Empty;
    public string EmbeddingModel { get; init; } = string.Empty;
}
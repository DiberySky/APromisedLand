namespace MafSampleApi.Models;

/// <summary>POST /api/chat 与 /api/chat/stream 的请求体。</summary>
public sealed record ChatRequestDto
{
    public string Message { get; init; } = string.Empty;

    /// <summary>可选；不传则服务端生成一个新的 GUID。</summary>
    public string? SessionId { get; init; }

    /// <summary>可选；不传则用 AgentOptions.ChatModel。</summary>
    public string? Model { get; init; }
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
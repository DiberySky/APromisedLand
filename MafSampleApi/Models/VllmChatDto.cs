namespace MafSampleApi.Models;

/// <summary>POST /api/vllm/chat 的请求体。</summary>
public sealed record VllmChatRequestDto
{
    public string Message { get; init; } = string.Empty;

    /// <summary>可选；不传则用 AgentOptions.ChatModel（默认 qwen3-4b-awq）。</summary>
    public string? Model { get; init; }

    /// <summary>可选；覆盖默认 SystemPrompt。</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>可选；覆盖默认 MaxOutputTokens（默认 2048）。</summary>
    public int? MaxOutputTokens { get; init; }
}

/// <summary>POST /api/vllm/chat 的响应体。</summary>
public sealed record VllmChatResponseDto
{
    public string Model { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}
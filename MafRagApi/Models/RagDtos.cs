using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using APromisedLand.Api.MafRag.Dtos;

namespace MafRagApi.Models;

/// <summary>POST /api/rag/chat 请求体。</summary>
public sealed class RagChatRequest
{
    [Required(AllowEmptyStrings = false)]
    public string Query { get; init; } = "";

    [Range(1, 20)]
    public int TopK { get; init; } = 5;

    public bool UseReranker { get; init; } = true;

    /// <summary>问答模式："rag"（检索增强，默认）或 "fulltext"（全文投喂 AI）。</summary>
    public string Mode { get; init; } = "rag";

    /// <summary>可选 JSON Schema，启用结构化输出。</summary>
    public string? ResponseSchema { get; init; }

    /// <summary>"text" 或 "json"。</summary>
    public string? OutputFormat { get; init; }

    /// <summary>是否启用原生结构化输出，默认 true。</summary>
    public bool UseStructuredOutput { get; init; } = true;
}

/// <summary>POST /api/rag/chat 响应体。</summary>
public sealed class RagChatResponse
{
    [JsonPropertyName("query")]   public string Query { get; init; } = "";
    [JsonPropertyName("answer")]  public string Answer { get; init; } = "";
    [JsonPropertyName("sources")] public List<RagHitDto> Sources { get; init; } = new();
    [JsonPropertyName("model")]   public string? Model { get; init; }
}

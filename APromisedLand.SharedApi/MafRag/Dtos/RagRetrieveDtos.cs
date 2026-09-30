using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace APromisedLand.Api.MafRag.Dtos;

/// <summary>POST /api/rag/retrieve 请求体。</summary>
public sealed class RagRetrieveRequest
{
    [Required(AllowEmptyStrings = false)]
    public string Query { get; init; } = "";

    [Range(1, 20)]
    public int TopK { get; init; } = 5;

    public bool UseReranker { get; init; } = true;
}

/// <summary>单个检索命中。</summary>
public sealed class RagHitDto
{
    [JsonPropertyName("chunkId")] public string ChunkId { get; init; } = "";
    [JsonPropertyName("docId")]   public string DocId { get; init; } = "";
    [JsonPropertyName("title")]   public string Title { get; init; } = "";

    /// <summary>JSON 扁平化后的路径，如 "二、小组简介.1. QC小组概况.小组名称"。</summary>
    [JsonPropertyName("path")]    public string? Path { get; init; }

    [JsonPropertyName("content")] public string Content { get; init; } = "";
    [JsonPropertyName("score")]   public double Score { get; init; }
}

/// <summary>POST /api/rag/retrieve 响应体。</summary>
public sealed class RagRetrieveResponse
{
    [JsonPropertyName("query")] public string Query { get; init; } = "";
    [JsonPropertyName("hits")]  public List<RagHitDto> Hits { get; init; } = new();
    [JsonPropertyName("totalChunks")] public int TotalChunks { get; init; }
}

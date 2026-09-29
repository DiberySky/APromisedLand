using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MafSampleApi.Models;

// ─── 摄入 ─────────────────────────────────────────────────────

/// <summary>POST /api/rag/ingest 请求体。</summary>
public sealed class RagIngestRequest
{
    /// <summary>文档标题（可选）。</summary>
    public string? Title { get; init; }

    /// <summary>
    /// 文档内容。可以是 JSON 字符串（自动扁平化分块），也可以是普通文本。
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Content { get; init; } = "";

    /// <summary>
    /// 单块最大字符数（仅对普通文本分块生效；JSON 按叶子节点天然分块）。
    /// </summary>
    [Range(100, 4000)]
    public int ChunkSize { get; init; } = 500;
}

/// <summary>POST /api/rag/ingest 响应体。</summary>
public sealed class RagIngestResponse
{
    [JsonPropertyName("docId")]      public string DocId { get; init; } = "";
    [JsonPropertyName("title")]      public string Title { get; init; } = "";
    [JsonPropertyName("chunkCount")] public int ChunkCount { get; init; }
    [JsonPropertyName("mode")]       public string Mode { get; init; } = "text";
}

// ─── 检索 ─────────────────────────────────────────────────────

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

// ─── RAG 对话 ─────────────────────────────────────────────────

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

// ─── 统计 ─────────────────────────────────────────────────────

public sealed class RagStatsResponse
{
    [JsonPropertyName("docCount")]    public int DocCount { get; init; }
    [JsonPropertyName("chunkCount")]  public int ChunkCount { get; init; }
}

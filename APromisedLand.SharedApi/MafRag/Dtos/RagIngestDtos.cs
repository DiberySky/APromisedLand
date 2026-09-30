using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace APromisedLand.Api.MafRag.Dtos;

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

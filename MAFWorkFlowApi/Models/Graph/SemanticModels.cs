using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MAFWorkFlowApi.Models.Graph;

// ─── 意图解析 ───
public sealed class ParseIntentRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Query { get; set; } = "";

    public List<string>? AvailableRelations { get; set; }
}

public sealed class IntentResult
{
    [JsonPropertyName("relation")]     public string? Relation { get; set; }
    [JsonPropertyName("direction")]    public string? Direction { get; set; }
    [JsonPropertyName("subjectName")]  public string? SubjectName { get; set; }
    [JsonPropertyName("confidence")]   public double Confidence { get; set; }
    [JsonPropertyName("reason")]       public string? Reason { get; set; }
    [JsonPropertyName("strategy")]     public string Strategy { get; set; } = "none";
    [JsonPropertyName("fallbackUsed")] public bool FallbackUsed { get; set; }
}

// ─── 边向量 ───
public sealed class CreateEdgeVectorRequest
{
    [Required] public Guid EdgeGuid { get; set; }
    [Required] public string Content { get; set; } = "";
    [Required] public List<float> Vector { get; set; } = [];
}

// ─── 语义搜索 ───
public sealed class SemanticSearchRequest
{
    [Required] public string Query { get; set; } = "";
    [Range(1, 100)] public int TopK { get; set; } = 10;
    [Range(0.0, 1.0)] public double? MinScore { get; set; }

    // ★ 阶段 2 新增：向量 / BM25 权重
    [Range(0.0, 1.0)] public double VectorWeight { get; set; } = 0.7;
    [Range(0.0, 1.0)] public double Bm25Weight   { get; set; } = 0.3;

    // ★ 阶段 1 预留（如果已经加了 Reranker，保留即可）
    public bool UseReranker { get; set; } = false;
}

public sealed class SemanticSearchHitDto
{
    public Guid NodeGuid { get; set; }
    public string NodeName { get; set; } = "";
    public double Score { get; set; }              // 综合分
    public double? VectorScore { get; set; }       // ★ 新增
    public double? Bm25Score { get; set; }         // ★ 新增
    public string? ViaEdgeName { get; set; }
    public string? Direction { get; set; }
    public string? MatchedContent { get; set; }
}

public sealed class SemanticSearchResponseDto
{
    public List<SemanticSearchHitDto> Hits { get; set; } = new();
    public IntentResult Intent { get; set; } = new();
    public object? Stats { get; set; }
}
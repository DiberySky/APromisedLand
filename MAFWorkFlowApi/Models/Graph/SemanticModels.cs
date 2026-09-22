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
    
    // ★ 阶段 4 新增
    /// <summary>跳数：1=单跳（默认），0=无限跳，2~5=指定跳数。</summary>
    [JsonPropertyName("hopCount")]
    public int HopCount { get; set; } = 1;

    /// <summary>"ancestors" / "descendants" / null（单跳）。</summary>
    [JsonPropertyName("aggregationMode")]
    public string? AggregationMode { get; set; }
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
    
    // ★ 新增：前端点击建议时，显式指定方向（覆盖 LLM 判断）
    /// <summary>"in" / "out" / null。非空时强制覆盖 LLM 的 direction。</summary>
    public string? DirectionOverride { get; set; }
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
    
    // ★ 阶段 4 新增
    [JsonPropertyName("hopDistance")]     public int? HopDistance { get; set; }
    [JsonPropertyName("isMultiHop")]      public bool IsMultiHop  { get; set; }
}

public sealed class SemanticSearchResponseDto
{
    public List<SemanticSearchHitDto> Hits { get; set; } = new();
    public IntentResult Intent { get; set; } = new();
    public object? Stats { get; set; }
    public string? Hint { get; set; }   // ★ 新增：友好提示
    // ★ 新增：基于图数据的建议
    public List<SuggestedRelationDto> Suggestions { get; set; } = new();
}

/// <summary>基于图数据的查询建议。</summary>
public sealed class SuggestedRelationDto
{
    /// <summary>关系名（如 SUBFIELD）。</summary>
    public string Relation { get; set; } = "";

    /// <summary>"in" / "out"。</summary>
    public string Direction { get; set; } = "";

    /// <summary>建议执行的查询文本（如"机器学习的SUBFIELD"）。</summary>
    public string Query { get; set; } = "";

    /// <summary>该关系下有几条边。</summary>
    public int Count { get; set; }

    /// <summary>示例节点名（最多 3 个）。</summary>
    public List<string> SampleNodes { get; set; } = new();
    
    // ★ 新增：友好标签（后端生成）
    public string DisplayLabel { get; set; } = "";    // 按钮上显示的文字
    public string Tooltip { get; set; } = "";         // hover 提示
}
using System.ComponentModel.DataAnnotations;

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
    public string? Relation { get; set; }
    public string? Direction { get; set; }      // "in" / "out"
    public string? SubjectName { get; set; }
    public double Confidence { get; set; }
    public string? Reason { get; set; }
    public string Strategy { get; set; } = "none";   // llm / rule / none
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
}

public sealed class SemanticSearchHitDto
{
    public Guid NodeGuid { get; set; }
    public string NodeName { get; set; } = "";
    public double Score { get; set; }
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
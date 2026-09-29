using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MafRagApi.Models.Graph;

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

    [JsonPropertyName("hopCount")]
    public int HopCount { get; set; } = 1;

    [JsonPropertyName("aggregationMode")]
    public string? AggregationMode { get; set; }
}

public sealed class SemanticSearchRequest
{
    [Required] public string Query { get; set; } = "";
    [Range(1, 100)] public int TopK { get; set; } = 10;
    [Range(0.0, 1.0)] public double? MinScore { get; set; }

    [Range(0.0, 1.0)] public double VectorWeight { get; set; } = 0.7;
    [Range(0.0, 1.0)] public double Bm25Weight   { get; set; } = 0.3;

    public bool UseReranker { get; set; } = true;

    /// <summary>"in" / "out" / null。非空时强制覆盖 LLM 的 direction。</summary>
    public string? DirectionOverride { get; set; }
}

public sealed class SemanticSearchHitDto
{
    public Guid NodeGuid { get; set; }
    public string NodeName { get; set; } = "";
    public double Score { get; set; }
    public double? VectorScore { get; set; }
    public double? Bm25Score { get; set; }
    public string? ViaEdgeName { get; set; }
    public string? Direction { get; set; }
    public string? MatchedContent { get; set; }

    [JsonPropertyName("hopDistance")] public int? HopDistance { get; set; }
    [JsonPropertyName("isMultiHop")]  public bool IsMultiHop  { get; set; }
}

public sealed class SemanticSearchResponseDto
{
    public List<SemanticSearchHitDto> Hits { get; set; } = new();
    public IntentResult Intent { get; set; } = new();
    public object? Stats { get; set; }
    public string? Hint { get; set; }
    public List<SuggestedRelationDto> Suggestions { get; set; } = new();
}

public sealed class SuggestedRelationDto
{
    public string Relation { get; set; } = "";
    public string Direction { get; set; } = "";
    public string Query { get; set; } = "";
    public int Count { get; set; }
    public List<string> SampleNodes { get; set; } = new();
    public string DisplayLabel { get; set; } = "";
    public string Tooltip { get; set; } = "";
}
using System.Text.Json.Serialization;

namespace ElasticsearchService.Models;

public class ElasticQuestion
{
    // [JsonPropertyName("id")]
    // public string Id { get; set; } = string.Empty;
    //
    // [JsonPropertyName("title")]
    // public string Title { get; set; } = string.Empty;
    //
    // [JsonPropertyName("content")]
    // public string Content { get; set; } = string.Empty;
    //
    // [JsonPropertyName("createdAt")]
    // public DateTimeOffset CreatedAt { get; set; }
    //
    // [JsonPropertyName("tags")]
    // public List<string> Tags { get; set; } = new();   // 统一用 List<string>

    [JsonPropertyName("id")]
    public required string Id { get; set; }
    [JsonPropertyName("title")]
    public required string Title { get; set; }
    [JsonPropertyName("content")]
    public required string Content { get; set; }
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("hasAcceptedAnswer")]
    public bool HasAcceptedAnswer { get; set; }
    [JsonPropertyName("answerCount")]
    public int AnswerCount { get; set; }
}
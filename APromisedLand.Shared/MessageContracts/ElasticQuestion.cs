using System.Text.Json.Serialization;

namespace APromisedLand.Shared.MessageContracts;

public class ElasticQuestion
{
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
    
    [JsonPropertyName("titleVector")]
    public float[]? TitleVector { get; set; }
    
    [JsonPropertyName("contentVector")]
    public float[]? ContentVector { get; set; }
}
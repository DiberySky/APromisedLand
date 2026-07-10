using System.Text.Json.Serialization;

namespace APromisedLand.Api.MessageContracts;

public class QuestionData
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
    
    // 向量字段，不在 _source 中存储（节省空间），仅用于索引
    [JsonPropertyName("embedding")]
    public float[]? Embedding { get; set; }
}
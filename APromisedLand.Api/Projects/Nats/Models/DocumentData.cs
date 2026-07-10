using System.Text.Json.Serialization;

namespace APromisedLand.Api.Projects.Nats.Models;

public class DocumentData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    [JsonPropertyName("titleVector")]
    public float[] TitleVector { get; set; } = Array.Empty<float>();  // 向量
    [JsonPropertyName("contentVector")]
    public float[] ContentVector { get; set; } = Array.Empty<float>();  // 向量
}
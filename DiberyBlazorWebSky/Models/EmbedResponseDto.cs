using System.Text.Json.Serialization;

public sealed class EmbedResponseDto
{
    [JsonPropertyName("vector")]
    public List<float> Vector { get; set; } = new();

    [JsonPropertyName("dimension")]
    public int Dimension { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";
}
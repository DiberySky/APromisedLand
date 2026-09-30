using System.Text.Json.Serialization;

namespace APromisedLand.Api.MafRag.Dtos;

public sealed class RagStatsResponse
{
    [JsonPropertyName("docCount")]    public int DocCount { get; init; }
    [JsonPropertyName("chunkCount")]  public int ChunkCount { get; init; }
}

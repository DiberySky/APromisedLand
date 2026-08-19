using System.Text.Json.Serialization;

namespace NebulaApi.Proxy.Dtos;

/// <summary>Submit a compact job. Mirrors <c>CompactIn</c>.</summary>
public sealed class CompactIn
{
    /// <summary>If omitted, compacts all spaces.</summary>
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    /// <summary>Optional sub-graph filter.</summary>
    [JsonPropertyName("graph")]
    public string? Graph { get; set; }
}

/// <summary>Submit a flush job. Mirrors <c>FlushIn</c>.</summary>
public sealed class FlushIn
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("graph")]
    public string? Graph { get; set; }
}

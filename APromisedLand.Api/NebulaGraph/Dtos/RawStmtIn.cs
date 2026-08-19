using System.Text.Json.Serialization;

namespace APromisedLand.Api.NebulaGraph.Dtos;

/// <summary>
/// Generic raw nGQL execution body. Mirrors
/// <c>app.schemas.common.RawStmtIn</c>.
/// </summary>
public sealed class RawStmtIn
{
    [JsonPropertyName("statement")]
    public string Statement { get; set; } = string.Empty;

    /// <summary>Switch to this space before running. May be omitted.</summary>
    [JsonPropertyName("space")]
    public string? Space { get; set; }
}

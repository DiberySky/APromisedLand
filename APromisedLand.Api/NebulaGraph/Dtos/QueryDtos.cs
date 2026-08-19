using System.Text.Json;
using System.Text.Json.Serialization;

namespace APromisedLand.Api.NebulaGraph.Dtos;

/// <summary>GO N STEPS OVER edge FROM vid [WHERE|YIELD]. Mirrors
/// <c>GoIn</c>. <see cref="FromVid"/> may be string or int64.</summary>
public sealed class GoIn
{
    [JsonPropertyName("space")]
    public string Space { get; set; } = string.Empty;

    [JsonPropertyName("steps")]
    public int Steps { get; set; } = 1;

    [JsonPropertyName("edge")]
    public string Edge { get; set; } = string.Empty;

    [JsonPropertyName("from_vid")]
    public object FromVid { get; set; } = string.Empty;

    /// <summary>BIDIRECT | OUT | IN</summary>
    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "BIDIRECT";

    [JsonPropertyName("where")]
    public string? Where { get; set; }

    /// <summary>YIELD expression. Maps to JSON key <c>yield</c>.</summary>
    [JsonPropertyName("yield")]
    public string? Yield { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("sample")]
    public int? Sample { get; set; }
}

/// <summary>FETCH PROP ON tag vids. Mirrors <c>FetchVertexIn</c>.
/// (The FastAPI service currently routes vertex fetches via the
/// <c>/spaces/{space}/vertices/fetch</c> endpoint that uses
/// <c>VertexFetchIn</c>; this DTO is provided for parity with the
/// underlying schema module.)</summary>
public sealed class FetchVertexIn
{
    [JsonPropertyName("space")]
    public string Space { get; set; } = string.Empty;

    [JsonPropertyName("vids")]
    public List<object> Vids { get; set; } = new();

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("yield")]
    public string? Yield { get; set; }
}

/// <summary>FETCH PROP ON edge pairs. Mirrors <c>FetchEdgeIn</c>.</summary>
public sealed class FetchEdgeIn
{
    [JsonPropertyName("space")]
    public string Space { get; set; } = string.Empty;

    [JsonPropertyName("edge")]
    public string Edge { get; set; } = string.Empty;

    [JsonPropertyName("pairs")]
    public List<Dictionary<string, object>> Pairs { get; set; } = new();

    [JsonPropertyName("yield")]
    public string? Yield { get; set; }
}

/// <summary>LOOKUP on tag/edge. Mirrors <c>LookupIn</c>.</summary>
public sealed class LookupIn
{
    [JsonPropertyName("space")]
    public string Space { get; set; } = string.Empty;

    /// <summary>"tag" or "edge"</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("where")]
    public string? Where { get; set; }

    [JsonPropertyName("yield")]
    public string? Yield { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }
}

/// <summary>FIND PATH / SHORTEST between two vertices. Mirrors
/// <c>FindPathIn</c>.</summary>
public sealed class FindPathIn
{
    [JsonPropertyName("space")]
    public string Space { get; set; } = string.Empty;

    [JsonPropertyName("src")]
    public object Src { get; set; } = string.Empty;

    [JsonPropertyName("dst")]
    public object Dst { get; set; } = string.Empty;

    [JsonPropertyName("edge")]
    public string? Edge { get; set; }

    [JsonPropertyName("steps")]
    public int Steps { get; set; } = 5;

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "BIDIRECT";

    /// <summary>Use SHORTEST instead of ALL.</summary>
    [JsonPropertyName("single_shortest")]
    public bool SingleShortest { get; set; } = false;

    [JsonPropertyName("with_prop")]
    public bool WithProp { get; set; } = true;

    [JsonPropertyName("no_loop")]
    public bool NoLoop { get; set; } = false;
}

/// <summary>GET SUBGRAPH around a vertex. Mirrors
/// <c>GetSubgraphIn</c>.</summary>
public sealed class GetSubgraphIn
{
    [JsonPropertyName("space")]
    public string Space { get; set; } = string.Empty;

    [JsonPropertyName("vid")]
    public object Vid { get; set; } = string.Empty;

    [JsonPropertyName("steps")]
    public int Steps { get; set; } = 1;

    [JsonPropertyName("in_edges")]
    public List<string>? InEdges { get; set; }

    [JsonPropertyName("out_edges")]
    public List<string>? OutEdges { get; set; }

    [JsonPropertyName("both_edges")]
    public List<string>? BothEdges { get; set; }

    [JsonPropertyName("with_prop")]
    public bool WithProp { get; set; } = true;
}

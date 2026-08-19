using System.Text.Json;
using System.Text.Json.Serialization;

namespace APromisedLand.Api.NebulaGraph.Dtos;

/// <summary>A single vertex to insert. Mirrors <c>VertexInsertItem</c>.
/// <see cref="Vid"/> may be either a string or an int64, so it is
/// declared as <see cref="object"/> and bound to a
/// <see cref="JsonElement"/> by System.Text.Json at runtime.</summary>
public sealed class VertexInsertItem
{
    [JsonPropertyName("vid")]
    public object Vid { get; set; } = string.Empty;

    /// <summary>Map of tag name -> property dict.</summary>
    [JsonPropertyName("tags")]
    public Dictionary<string, Dictionary<string, object>> Tags { get; set; } = new();
}

public sealed class VertexInsertIn
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("vertices")]
    public List<VertexInsertItem> Vertices { get; set; } = new();

    [JsonPropertyName("if_not_exists")]
    public bool IfNotExists { get; set; } = true;
}

public sealed class VertexFetchIn
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("vids")]
    public List<object> Vids { get; set; } = new();

    /// <summary>Filter properties to a specific tag; omit to fetch with
    /// all tags.</summary>
    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("prop")]
    public string? Prop { get; set; }
}

public sealed class VertexDeleteIn
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("vids")]
    public List<object> Vids { get; set; } = new();
}

/// <summary>UPSERT VERTEX body. Mirrors <c>VertexUpsertIn</c>.</summary>
public sealed class VertexUpsertIn
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("vid")]
    public object Vid { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    /// <summary>nGQL SET expressions, e.g. {"age": "age+1"}.</summary>
    [JsonPropertyName("set")]
    public Dictionary<string, string> Set { get; set; } = new();

    /// <summary>WHEN condition expression.</summary>
    [JsonPropertyName("when")]
    public string? When { get; set; }

    [JsonPropertyName("if_not_exists")]
    public bool IfNotExists { get; set; } = false;

    [JsonPropertyName("update")]
    public bool Update { get; set; } = true;
}

/// <summary>UPDATE VERTEX ON tag vid SET ...</summary>
public sealed class VertexUpdateIn
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("vid")]
    public object Vid { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("set")]
    public Dictionary<string, string> Set { get; set; } = new();

    [JsonPropertyName("when")]
    public string? When { get; set; }
}

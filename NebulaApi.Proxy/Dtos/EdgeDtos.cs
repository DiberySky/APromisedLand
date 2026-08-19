using System.Text.Json;
using System.Text.Json.Serialization;

namespace NebulaApi.Proxy.Dtos;

/// <summary>A single edge to insert. Mirrors <c>EdgeInsertItem</c>.
/// <see cref="Src"/> and <see cref="Dst"/> may be string or int64,
/// so they are typed as <see cref="object"/> (bound to a
/// <see cref="JsonElement"/> at runtime).</summary>
public sealed class EdgeInsertItem
{
    [JsonPropertyName("src")]
    public object Src { get; set; } = string.Empty;

    [JsonPropertyName("dst")]
    public object Dst { get; set; } = string.Empty;

    [JsonPropertyName("ranking")]
    public int Ranking { get; set; } = 0;

    [JsonPropertyName("props")]
    public Dictionary<string, object> Props { get; set; } = new();
}

public sealed class EdgeInsertIn
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("edge")]
    public string Edge { get; set; } = string.Empty;

    [JsonPropertyName("edges")]
    public List<EdgeInsertItem> Edges { get; set; } = new();

    [JsonPropertyName("if_not_exists")]
    public bool IfNotExists { get; set; } = true;
}

public sealed class EdgeFetchIn
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("edge")]
    public string Edge { get; set; } = string.Empty;

    /// <summary>List of {"src":..,"dst":..,"ranking":0} objects.</summary>
    [JsonPropertyName("pairs")]
    public List<Dictionary<string, object>> Pairs { get; set; } = new();

    [JsonPropertyName("prop")]
    public string? Prop { get; set; }
}

public sealed class EdgeDeleteIn
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("edge")]
    public string Edge { get; set; } = string.Empty;

    [JsonPropertyName("pairs")]
    public List<Dictionary<string, object>> Pairs { get; set; } = new();
}

public sealed class EdgeUpdateIn
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("edge")]
    public string Edge { get; set; } = string.Empty;

    [JsonPropertyName("src")]
    public object Src { get; set; } = string.Empty;

    [JsonPropertyName("dst")]
    public object Dst { get; set; } = string.Empty;

    [JsonPropertyName("ranking")]
    public int Ranking { get; set; } = 0;

    [JsonPropertyName("set")]
    public Dictionary<string, string> Set { get; set; } = new();

    [JsonPropertyName("when")]
    public string? When { get; set; }
}

public sealed class EdgeUpsertIn
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("edge")]
    public string Edge { get; set; } = string.Empty;

    [JsonPropertyName("src")]
    public object Src { get; set; } = string.Empty;

    [JsonPropertyName("dst")]
    public object Dst { get; set; } = string.Empty;

    [JsonPropertyName("ranking")]
    public int Ranking { get; set; } = 0;

    [JsonPropertyName("set")]
    public Dictionary<string, string> Set { get; set; } = new();

    [JsonPropertyName("when")]
    public string? When { get; set; }

    [JsonPropertyName("if_not_exists")]
    public bool IfNotExists { get; set; } = false;

    [JsonPropertyName("update")]
    public bool Update { get; set; } = true;
}

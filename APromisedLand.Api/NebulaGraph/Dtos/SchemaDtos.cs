using System.Text.Json.Serialization;

namespace APromisedLand.Api.NebulaGraph.Dtos;

/// <summary>
/// A single tag / edge property definition. Mirrors
/// <c>PropertyDef</c> from <c>app.schemas.schema</c>.
/// </summary>
public sealed class PropertyDef
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>e.g. string, int64, double, bool, date,
    /// fixed_string(16), geography</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("nullable")]
    public bool Nullable { get; set; } = true;

    /// <summary>Raw nGQL default expression.</summary>
    [JsonPropertyName("default")]
    public string? Default { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

/// <summary>
/// Base for tag / edge create bodies. Mirrors
/// <c>_SchemaCreateBase</c>. The <see cref="Space"/> field is
/// always overridden by the controller with the path parameter.
/// </summary>
public abstract class SchemaCreateBase
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("properties")]
    public List<PropertyDef> Properties { get; set; } = new();

    /// <summary>seconds, 0 disables TTL</summary>
    [JsonPropertyName("ttl_duration")]
    public int? TtlDuration { get; set; }

    [JsonPropertyName("ttl_col")]
    public string? TtlCol { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("if_not_exists")]
    public bool IfNotExists { get; set; } = true;
}

public sealed class TagCreateIn : SchemaCreateBase { }
public sealed class EdgeCreateIn : SchemaCreateBase { }

/// <summary>ALTER TAG / EDGE body. Mirrors <c>AlterSchemaIn</c>.</summary>
public sealed class AlterSchemaIn
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>"tag" or "edge". Always set by the controller.</summary>
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("add")]
    public List<PropertyDef> Add { get; set; } = new();

    [JsonPropertyName("change")]
    public List<PropertyDef> Change { get; set; } = new();

    [JsonPropertyName("drop")]
    public List<string> Drop { get; set; } = new();

    [JsonPropertyName("ttl_duration")]
    public int? TtlDuration { get; set; }

    [JsonPropertyName("ttl_col")]
    public string? TtlCol { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

/// <summary>Base for tag / edge index create bodies. Mirrors
/// <c>_IndexCreateBase</c>.</summary>
public abstract class IndexCreateBase
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Property names, optionally with length, e.g.
    /// ["name(64)"].</summary>
    [JsonPropertyName("fields")]
    public List<string> Fields { get; set; } = new();

    [JsonPropertyName("if_not_exists")]
    public bool IfNotExists { get; set; } = true;

    [JsonPropertyName("rebuild")]
    public bool Rebuild { get; set; } = true;
}

public sealed class TagIndexCreateIn : IndexCreateBase
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;
}

public sealed class EdgeIndexCreateIn : IndexCreateBase
{
    [JsonPropertyName("edge")]
    public string Edge { get; set; } = string.Empty;
}

/// <summary>Create a fulltext index. Mirrors <c>FulltextIndexCreateIn</c>.</summary>
public sealed class FulltextIndexCreateIn
{
    [JsonPropertyName("space")]
    public string? Space { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>"tag" or "edge"</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>The tag or edge to index.</summary>
    [JsonPropertyName("schema_name")]
    public string SchemaName { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public List<string> Fields { get; set; } = new();

    [JsonPropertyName("if_not_exists")]
    public bool IfNotExists { get; set; } = true;
}

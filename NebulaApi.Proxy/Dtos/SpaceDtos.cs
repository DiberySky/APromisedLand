using System.Text.Json.Serialization;

namespace NebulaApi.Proxy.Dtos;

/// <summary>CREATE SPACE body. Mirrors <c>SpaceCreateIn</c>.</summary>
public sealed class SpaceCreateIn
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("partition_num")]
    public int PartitionNum { get; set; } = 100;

    [JsonPropertyName("replica_factor")]
    public int ReplicaFactor { get; set; } = 1;

    /// <summary>e.g. FIXED_STRING(16) or INT64</summary>
    [JsonPropertyName("vid_type")]
    public string VidType { get; set; } = "FIXED_STRING(8)";

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("if_not_exists")]
    public bool IfNotExists { get; set; } = true;
}

/// <summary>Body for the alter-space-comment endpoint.</summary>
public sealed class SpaceAlterCommentIn
{
    [JsonPropertyName("comment")]
    public string Comment { get; set; } = string.Empty;
}

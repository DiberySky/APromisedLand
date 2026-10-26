using System.Text.Json;

namespace TreeGraph.Shared.EdgeSky.Dtos;

/// <summary>边实例视图：唯一标识 = 五元组 &lt;SpaceId, EdgeType, SrcVid, Rank, DstVid&gt;（Nebula 对齐）。</summary>
public class EdgeDto
{
    /// <summary>所属图空间 Id。</summary>
    public string SpaceId { get; set; } = string.Empty;

    public string EdgeType { get; set; } = string.Empty;
    public string SrcVid { get; set; } = string.Empty;
    public long Rank { get; set; }
    public string DstVid { get; set; } = string.Empty;

    /// <summary>属性值包（仅含 Schema 内键；null/空 = 无属性）。</summary>
    public Dictionary<string, JsonElement>? Props { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// UPSERT 边请求（覆盖语义，对齐 INSERT EDGE）：
/// Props 为最终全集；未列出的 Schema 属性落 DEFAULT，无 DEFAULT 则 NULL。
/// </summary>
public class UpsertEdgeRequest
{
    public Dictionary<string, JsonElement>? Props { get; set; }
}

/// <summary>
/// PATCH 边请求（部分更新，对齐 UPDATE EDGE SET）：
/// 仅更新列出的键；值 null = 清除该属性（落 NULL）。
/// </summary>
public class PatchEdgeRequest
{
    public Dictionary<string, JsonElement?> Props { get; set; } = [];
}

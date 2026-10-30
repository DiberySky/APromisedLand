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

    /// <summary>起点节点名称（查询时 join nodes 补充，仅展示用）。</summary>
    public string? SrcName { get; set; }

    /// <summary>终点节点名称（查询时 join nodes 补充，仅展示用）。</summary>
    public string? DstName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>节点所属图空间解析结果（节点关系维护用：边从属于图空间）。</summary>
public class NodeSpaceDto
{
    public string SpaceId { get; set; } = string.Empty;
    public string SpaceName { get; set; } = string.Empty;

    /// <summary>空间类型（Dynamic/Type/Unit/Knowledge，与后端 SpaceType 枚举同名）。</summary>
    public string SpaceType { get; set; } = string.Empty;
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

/// <summary>节点搜索轻量结果（对端节点选择用；仅暴露 Id/Name/HostType，避免泄露 Node 全字段）。</summary>
public class NodeLookupDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }

    /// <summary>宿主类型（UnitTree/CategoryTree/KnowledgeTree/SpaceTree/Line/LineNode 等），UI 按空间类型过滤候选。</summary>
    public string? HostType { get; set; }
}

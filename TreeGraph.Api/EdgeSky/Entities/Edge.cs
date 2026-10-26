using System.Text.Json;

namespace TreeGraph.Api.EdgeSky.Entities;

/// <summary>
/// 边实例（顶级实体，镜像 NebulaGraph）：唯一标识 = 五元组
/// &lt;SpaceId, EdgeType, 起点 VID, Rank, 终点 VID&gt;（复合主键；边从属于图空间）。
/// 属性值 = props jsonb 单列，属性集由所属 EdgeType 的 <see cref="EdgeTypeProp"/> Schema 校验；
/// 写入为覆盖语义（未列出的属性落 DEFAULT，无 DEFAULT 则 NULL）。
/// </summary>
public class Edge
{
    /// <summary>所属图空间 Id（与 EdgeType 共同 FK → edge_types，Restrict）。</summary>
    public string SpaceId { get; set; } = string.Empty;

    /// <summary>边类型名（与 SpaceId 共同 FK → edge_types，Restrict）。</summary>
    public string EdgeType { get; set; } = string.Empty;

    /// <summary>起点节点 Id（nodes.id，FK Restrict）。</summary>
    public string SrcVid { get; set; } = string.Empty;

    /// <summary>终点节点 Id（nodes.id，FK Restrict）。</summary>
    public string DstVid { get; set; } = string.Empty;

    /// <summary>边等级（int64，区分同起终点同类型的多条边；默认 0）。</summary>
    public long Rank { get; set; }

    /// <summary>属性值包（jsonb 对象；null = 无属性）。</summary>
    public JsonElement? Props { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

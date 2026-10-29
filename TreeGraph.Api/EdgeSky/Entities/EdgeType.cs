namespace TreeGraph.Api.EdgeSky.Entities;

/// <summary>
/// EdgeType（边类型）：镜像 NebulaGraph 语义 —— 空间内唯一（从属于一个图空间）、
/// 创建后不可改名，定义 = 名称 + 属性 Schema（<see cref="EdgeTypeProp"/> 子表）+ TTL + 注释。
/// 主键 = (SpaceId, Name)（不可改名，故可直接作 PK）。
/// </summary>
public class EdgeType
{
    /// <summary>所属图空间 Id（FK → spaces；Nebula：EdgeType 是空间级对象）。</summary>
    public string SpaceId { get; set; } = string.Empty;

    /// <summary>边类型名（空间内唯一，对应 nGQL 的 edge_type_name；Nebula 命名规则由服务层校验）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>边类型级注释（nGQL COMMENT）。</summary>
    public string? Comment { get; set; }

    /// <summary>中文显示名称（仅 UI 展示用，Nebula 无此概念）。</summary>
    public string? DisplayName { get; set; }

    /// <summary>展示序（仅 UI 排序用，空间内手动调整；不参与图 Schema）。</summary>
    public int DisplayOrder { get; set; }

    /// <summary>TTL 列名（须为本类型的 INT/TIMESTAMP 属性；与 TtlDurationSeconds 成对出现）。</summary>
    public string? TtlCol { get; set; }

    /// <summary>TTL 时长（秒，nGQL TTL_DURATION）。</summary>
    public long? TtlDurationSeconds { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>属性 Schema（本类型预定义的属性集合）。</summary>
    public List<EdgeTypeProp> Props { get; set; } = [];
}

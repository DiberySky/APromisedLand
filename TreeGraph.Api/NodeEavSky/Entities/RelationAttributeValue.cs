namespace TreeGraph.Api.NodeEavSky.Entities;

/// <summary>
/// 关系属性值表（relation 类型独立存储）：一条记录 = 一条有向边（宿主实体 → 目标节点）。
///
/// 与 <see cref="AttributeValue"/> 的类型化列模式分离，因为「指向哪个节点」是强结构数据，
/// 需要外键保证目标存在，并投影到 NebulaGraph。一个关系属性在单个实体上最多一个值
/// （唯一索引 uq_rav_entity_attr）；同一图边类型下起点不能重复指向同一终点
/// （唯一索引 uq_rav_type_endpoints，防 NebulaGraph (src,dst,type,rank=0) 覆盖）。
/// </summary>
public class RelationAttributeValue
{
    /// <summary>值 Id（空字符串触发 DB DEFAULT gen_random_uuid()，HasSentinel 语义），也是图边实例 id。</summary>
    public string ValueId { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string AttributeId { get; set; } = "";

    /// <summary>图边类型（冗余自属性定义，投影时直接使用）。</summary>
    public string RelationType { get; set; } = "";

    /// <summary>目标节点 Id（nodes 中真实节点，FK Restrict）。</summary>
    public string ToNodeId { get; set; } = "";

    /// <summary>边实例展示名（可空，如「主供」「备用」）。</summary>
    public string? Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AttributeDefinition Attribute { get; set; } = null!;
}

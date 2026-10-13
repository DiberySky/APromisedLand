namespace TreeGraph.Api.NodeEavSky.Entities;

/// <summary>
/// 边类型定义（Edge Definition）。
///
/// 类似 CompositeType 的定位：定义一种关系类型，TypeName 直接作为
/// EAV attribute_catalog.entity_type 写入，边实例的属性值走现有 EAV 管道。
///
/// 约束：
///   - TypeName 全局唯一（PascalCase，如 "Collaboration"）
///   - IsGlobal=true 时所有节点类型可用，否则仅 FromNodeType/ToNodeType 指定的可用
///   - IsDirected=true 表示有方向（from→to），false 表示无方向（边等同双向）
/// </summary>
public class EdgeDefinition
{
    public string EdgeTypeId { get; set; } = "";

    /// <summary>PascalCase 类型名，如 Collaboration。作为 EAV entityType 使用。</summary>
    public string TypeName { get; set; } = "";

    /// <summary>中文显示名，如 协作关系。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>可选：起点节点类型过滤（如 UnitTree、CategoryTree）。null 表示不限。</summary>
    public string? FromNodeType { get; set; }

    /// <summary>可选：终点节点类型过滤。null 表示不限。</summary>
    public string? ToNodeType { get; set; }

    /// <summary>全局共享，默认 true。</summary>
    public bool IsGlobal { get; set; } = true;

    /// <summary>有向，默认 true。</summary>
    public bool IsDirected { get; set; } = true;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 是否为链式结构类型（如输电线路、物流线路）。
    /// 链式结构支持：批量创建链、完整路径查询（WITH RECURSIVE）、
    /// 入度/成环校验、连通分量。
    /// </summary>
    public bool IsChain { get; set; } = false;

    /// <summary>
    /// 严格线性链式（入度 ≤ 1，无环）。true 时创建边会自动校验入度约束。
    /// false 时允许 T 接分支（入度 ≤ 1 但出度可多）。
    /// 仅 IsChain=true 时生效。
    /// </summary>
    public bool IsLinear { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<EdgeInstance> Instances { get; set; } = new();
}

/// <summary>
/// 边实例（Edge Instance）。
///
/// 两个节点之间的具体关系。节点 ID 为字符串，不做 FK 约束（因为不同实体
/// 分布在不同表：UnitTree、CategoryTree 等），应用层根据 EdgeDefinition
/// 的 FromNodeType/ToNodeType 做存在性校验。
///
/// 属性值复用 EAV：entity_type = EdgeDefinition.TypeName，entity_id = EdgeId。
/// </summary>
public class EdgeInstance
{
    public string EdgeId { get; set; } = "";

    public string EdgeTypeId { get; set; } = "";
    public EdgeDefinition EdgeType { get; set; } = null!;

    /// <summary>起点节点 ID（字符串）。</summary>
    public string FromNodeId { get; set; } = "";

    /// <summary>终点节点 ID（字符串）。</summary>
    public string ToNodeId { get; set; } = "";

    /// <summary>可选权重（如相似度、距离、强度）。</summary>
    public decimal? Weight { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

using TreeGraph.Shared.NodeEavSky.Abstractions;

namespace TreeGraph.Api.EdgeSky.Entities;

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

    /// <summary>
    /// 全局共享，默认 true。false 时仅 FromNodeType/ToNodeType 指定的节点类型可用。
    /// 当前实现：Create/Update 时强约束——IsGlobal=false 必须同时声明
    /// FromNodeType 与 ToNodeType，否则拒绝（无节点类型即无约束，等同全局）；
    /// 建边时由 IEdgeNodeValidator 按声明的节点类型做存在性/类型校验。
    /// </summary>
    public bool IsGlobal { get; set; } = true;

    /// <summary>
    /// 有向，默认 true。false 表示无方向（边等同双向）。
    /// 当前实现：创建时反向亦视为重复；出边/入边查询与 BFS 遍历均做对称化展开
    /// （无向时忽略 Direction，按双向处理）。路径/线路查询（LineService）仍按存储的
    /// from→to 处理，无向线路场景未对称化（语义预留）。
    /// </summary>
    public bool IsDirected { get; set; } = true;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 是否为线路结构类型（如输电线路、物流线路）。
    /// 线路结构支持：批量创建线路、完整路径查询（WITH RECURSIVE）、
    /// 入度/成环校验、连通分量。
    /// </summary>
    public bool IsLine { get; set; } = false;

    /// <summary>
    /// 严格线性线路（入度 ≤ 1，无环）。true 时创建边会自动校验入度约束。
    /// false 时允许 T 接分支（入度 ≤ 1 但出度可多）。
    /// 仅 IsLine=true 时生效。
    /// </summary>
    public bool IsLinear { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<EdgeInstance> Instances { get; set; } = new();

    /// <summary>供配电场景内置边类型种子（18 条）。</summary>
    public static List<EdgeDefinition> SeedData() =>
    [
        new EdgeDefinition
        {
            EdgeTypeId = "e0000001-0000-4000-8000-000000000001",
            TypeName = "ConnectedTo",
            DisplayName = "电气连接",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = false,
            IsLine = false,
            IsLinear = false,
            Description = "设备之间的电气连通（母线↔断路器、隔离开关↔线路）；物理连接对称，按无向处理。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000002-0000-4000-8000-000000000002",
            TypeName = "SuppliedBy",
            DisplayName = "供电来源",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = true,
            IsLine = true,
            IsLinear = false,
            Description = "下游设备指向供电上游，沿供电方向逐级上溯；线路结构支持完整供电路径查询与成环校验。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000003-0000-4000-8000-000000000003",
            TypeName = "LowVoltageAccess",
            DisplayName = "低压接入",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = true,
            IsLine = false,
            IsLinear = false,
            Description = "低压电缆组接入变压器，与电气连接配合还原「建筑—低压柜—变压器」链路。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000004-0000-4000-8000-000000000004",
            TypeName = "TransmissionLine",
            DisplayName = "输电线路",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = true,
            IsLine = true,
            IsLinear = false,
            Description = "线路段序列，属线路结构；允许 T 接分支（不限制入度）。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000005-0000-4000-8000-000000000005",
            TypeName = "Feeder",
            DisplayName = "馈线",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = true,
            IsLine = true,
            IsLinear = true,
            Description = "单电源馈线，线路结构且严格线性（入度≤1、无环）。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000006-0000-4000-8000-000000000006",
            TypeName = "BelongsTo",
            DisplayName = "逻辑归属",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = true,
            IsLine = false,
            IsLinear = false,
            Description = "设备在管理上归属台区/馈线等逻辑单元；与电气连接、物理位置分开建模。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000007-0000-4000-8000-000000000007",
            TypeName = "AssignedTo",
            DisplayName = "归属单位",
            FromNodeType = "SpaceTree",
            ToNodeType = "UnitTree",
            IsGlobal = false,
            IsDirected = true,
            IsLine = false,
            IsLinear = false,
            Description = "设备归属运维单位，与逻辑归属分型，避免同一对点混用一条边。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000008-0000-4000-8000-000000000008",
            TypeName = "InstalledIn",
            DisplayName = "安装承载",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = true,
            IsLine = false,
            IsLinear = false,
            Description = "分布式光伏、储能等设备安装在建筑/站点上。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000009-0000-4000-8000-000000000009",
            TypeName = "AdjacentTo",
            DisplayName = "空间邻接",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = false,
            IsLine = false,
            IsLinear = false,
            Description = "空间实体相邻（共享边界/距离），用于台区划分与抢修路径分析；对称按无向处理。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000010-0000-4000-8000-000000000010",
            TypeName = "LocatedAt",
            DisplayName = "位置所在",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = true,
            IsLine = false,
            IsLinear = false,
            Description = "设备物理位置（站房/楼层/房间），与逻辑归属分离。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000011-0000-4000-8000-000000000011",
            TypeName = "Protects",
            DisplayName = "保护范围",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = true,
            IsLine = false,
            IsLinear = false,
            Description = "保护装置指向被保护设备，用于故障时定位应动作的保护。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000012-0000-4000-8000-000000000012",
            TypeName = "BacksUp",
            DisplayName = "保护配合",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = true,
            IsLine = false,
            IsLinear = false,
            Description = "上下级保护配合（配合级差与时间定值），支持定值校核与越级跳闸分析。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000013-0000-4000-8000-000000000013",
            TypeName = "PrerequisiteFor",
            DisplayName = "操作前置",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = true,
            IsLine = false,
            IsLinear = false,
            Description = "操作票与五防规则的前置约束，例如线路带电时禁止合接地刀闸。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000014-0000-4000-8000-000000000014",
            TypeName = "DependsOn",
            DisplayName = "二次依赖",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = true,
            IsLine = false,
            IsLinear = false,
            Description = "二次设备（保护、测控、通信）依赖一次设备或电源，用于故障溯源。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000015-0000-4000-8000-000000000015",
            TypeName = "Affects",
            DisplayName = "影响范围",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = true,
            IsLine = false,
            IsLinear = false,
            Description = "故障事件节点指向受影响的设备/用户，表达失电或降级范围。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000016-0000-4000-8000-000000000016",
            TypeName = "TrippedBy",
            DisplayName = "事件起因",
            FromNodeType = "SpaceTree",
            ToNodeType = "SpaceTree",
            IsGlobal = false,
            IsDirected = true,
            IsLine = false,
            IsLinear = false,
            Description = "跳闸事件指向触发它的保护动作或根因故障，用于事后复盘。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000017-0000-4000-8000-000000000017",
            TypeName = "PassesThrough",
            DisplayName = "跨越通过",
            FromNodeType = null,
            ToNodeType = "LineNode",
            IsGlobal = true,
            IsDirected = true,
            IsLine = false,
            IsLinear = false,
            Description = "线路区段 → 交叉跨越点；空间相交但电气隔离，不参与供电拓扑遍历。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new EdgeDefinition
        {
            EdgeTypeId = "e0000018-0000-4000-8000-000000000018",
            TypeName = "LineSegment",
            DisplayName = "线路段",
            FromNodeType = "LineNode",
            ToNodeType = "LineNode",
            IsGlobal = false,
            IsDirected = false,
            IsLine = true,
            IsLinear = false,
            Description = "两个跨越点（LineNode）之间的线路区段；无向对称，属线路结构，可串联成完整路径，允许 T 接分支。",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        }
    ];
}

/// <summary>
/// 边实例（Edge Instance）。
///
/// 两个节点之间的具体关系。节点 ID 为字符串，不做 FK 约束（因为不同实体
/// 分布在不同表：UnitTree、CategoryTree 等），应用层由 <see cref="Services.IEdgeNodeValidator"/>
/// 依据 EdgeDefinition 的 FromNodeType/ToNodeType 做存在性/类型校验。
///
/// 属性值复用 EAV：entity_type = EdgeDefinition.TypeName，entity_id = EdgeId。
/// </summary>
public class EdgeInstance : Inode
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

    /// <summary>
    /// 镜像自 EdgeDefinition（IsLine &amp;&amp; IsLinear）的"严格线性"标记。
    /// 供数据库层部分唯一索引 uq_ei_single_incoming 使用：同一 edge_type_id 下
    /// 同一 to_node_id 至多一条边（入度 ≤ 1），把"查后插"的并发竞态收敛为数据库原子约束。
    /// </summary>
    public bool EnforceSingleIncoming { get; set; }

    /// <summary>边的显示名称（可选，为空时由调用方拼装 from→to）。</summary>
    public string? DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // ── Inode 显式实现 ──────────────────────────────────────────────
    string Inode.Id => EdgeId;
    string Inode.Name => DisplayName ?? $"{FromNodeId}→{ToNodeId}";
}

using System.Text.Json;
using TreeGraph.Shared.NodeEavSky;

namespace TreeGraph.Api.NodeEavSky.Entities;

/// <summary>属性元数据（属性目录）</summary>
public class AttributeDefinition
{
    public string AttributeId { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string AttributeName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DataType { get; set; } = "string";

    public bool IsRequired { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsSortable { get; set; }
    public bool IsDeleted { get; set; }
    public int Version { get; set; } = 1;
    public int DisplayOrder { get; set; }

    /// <summary>枚举约束（JSON 数组），映射 jsonb</summary>
    public JsonDocument? AllowedValues { get; set; }

    /// <summary>验证规则（JSON 对象）</summary>
    public JsonDocument? ValidationRule { get; set; }

    public string? DefaultValue { get; set; }

    /// <summary>组合类型关联（当 DataType = "composite" 时）</summary>
    public string? RefCompositeTypeId { get; set; }
    public CompositeTypeDefinition? RefCompositeType { get; set; }

    /// <summary>表结构关联（当 DataType = "table" 时，行数据存 custom_table_rows）</summary>
    public string? RefTableDefinitionId { get; set; }
    public CustomTableDefinition? RefTableDefinition { get; set; }

    /// <summary>
    /// 时序记录模式（仅 DataType = "table" 时有效）：
    /// 所引表活动列第 1 列须 date、第 2 列须 time；行列表屏蔽序号列。
    /// 取代原独立 DataType "record"。
    /// </summary>
    public bool IsRecordMode { get; set; }

    /// <summary>选项集关联（当 DataType = "single_choice" 时）</summary>
    public string? RefOptionSetId { get; set; }
    public OptionSet? RefOptionSet { get; set; }

    /// <summary>数值属性绑定的单位（指向 unit_trees 节点 Id，仅作记录不参与换算）</summary>
    public string? UnitId { get; set; }
    public TreeGraph.Shared.TreeEavSky.Entities.UnitTree? Unit { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>供配电边类型的内置示例属性种子（31 条）。</summary>
    public static List<AttributeDefinition> SeedData() =>
    [
        new AttributeDefinition
        {
            AttributeId = "a0000001-0000-4000-8000-000000000001",
            EntityType = "ConnectedTo",
            AttributeName = "voltageLevel",
            DisplayName = "电压等级",
            DataType = EavDataTypes.String,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000002-0000-4000-8000-000000000002",
            EntityType = "ConnectedTo",
            AttributeName = "connectionMode",
            DisplayName = "连接方式",
            DataType = EavDataTypes.String,
            DisplayOrder = 1,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000003-0000-4000-8000-000000000003",
            EntityType = "SuppliedBy",
            AttributeName = "supplyDirection",
            DisplayName = "供电方向",
            DataType = EavDataTypes.String,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000004-0000-4000-8000-000000000004",
            EntityType = "SuppliedBy",
            AttributeName = "isPrimarySupply",
            DisplayName = "是否主供",
            DataType = EavDataTypes.Bool,
            DisplayOrder = 1,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000005-0000-4000-8000-000000000005",
            EntityType = "LowVoltageAccess",
            AttributeName = "cableSegmentCount",
            DisplayName = "电缆段数",
            DataType = EavDataTypes.Int,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000006-0000-4000-8000-000000000006",
            EntityType = "LowVoltageAccess",
            AttributeName = "cableLength",
            DisplayName = "电缆长度",
            DataType = EavDataTypes.Decimal,
            DisplayOrder = 1,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000007-0000-4000-8000-000000000007",
            EntityType = "TransmissionLine",
            AttributeName = "voltageLevel",
            DisplayName = "电压等级",
            DataType = EavDataTypes.String,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000008-0000-4000-8000-000000000008",
            EntityType = "TransmissionLine",
            AttributeName = "commissionDate",
            DisplayName = "投运日期",
            DataType = EavDataTypes.Date,
            DisplayOrder = 1,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000009-0000-4000-8000-000000000009",
            EntityType = "Feeder",
            AttributeName = "ratedCurrent",
            DisplayName = "额定载流量",
            DataType = EavDataTypes.Decimal,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000010-0000-4000-8000-000000000010",
            EntityType = "Feeder",
            AttributeName = "isSingleSource",
            DisplayName = "单电源",
            DataType = EavDataTypes.Bool,
            DisplayOrder = 1,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000011-0000-4000-8000-000000000011",
            EntityType = "BelongsTo",
            AttributeName = "belongType",
            DisplayName = "归属类型",
            DataType = EavDataTypes.String,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000012-0000-4000-8000-000000000012",
            EntityType = "BelongsTo",
            AttributeName = "effectiveDate",
            DisplayName = "生效日期",
            DataType = EavDataTypes.Date,
            DisplayOrder = 1,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000013-0000-4000-8000-000000000013",
            EntityType = "AssignedTo",
            AttributeName = "assignType",
            DisplayName = "归属类型",
            DataType = EavDataTypes.String,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000014-0000-4000-8000-000000000014",
            EntityType = "InstalledIn",
            AttributeName = "installYear",
            DisplayName = "安装年份",
            DataType = EavDataTypes.Int,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000015-0000-4000-8000-000000000015",
            EntityType = "InstalledIn",
            AttributeName = "installedCapacity",
            DisplayName = "装机容量",
            DataType = EavDataTypes.Decimal,
            DisplayOrder = 1,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000016-0000-4000-8000-000000000016",
            EntityType = "AdjacentTo",
            AttributeName = "distance",
            DisplayName = "距离",
            DataType = EavDataTypes.Decimal,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000017-0000-4000-8000-000000000017",
            EntityType = "AdjacentTo",
            AttributeName = "shareBoundary",
            DisplayName = "共享边界",
            DataType = EavDataTypes.Bool,
            DisplayOrder = 1,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000018-0000-4000-8000-000000000018",
            EntityType = "LocatedAt",
            AttributeName = "position",
            DisplayName = "位置",
            DataType = EavDataTypes.Location,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000019-0000-4000-8000-000000000019",
            EntityType = "Protects",
            AttributeName = "protectionType",
            DisplayName = "保护类型",
            DataType = EavDataTypes.String,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000020-0000-4000-8000-000000000020",
            EntityType = "Protects",
            AttributeName = "settingValue",
            DisplayName = "保护定值",
            DataType = EavDataTypes.String,
            DisplayOrder = 1,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000021-0000-4000-8000-000000000021",
            EntityType = "BacksUp",
            AttributeName = "coordinationGap",
            DisplayName = "配合级差",
            DataType = EavDataTypes.Decimal,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000022-0000-4000-8000-000000000022",
            EntityType = "BacksUp",
            AttributeName = "timeSetting",
            DisplayName = "时间定值",
            DataType = EavDataTypes.Decimal,
            DisplayOrder = 1,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000023-0000-4000-8000-000000000023",
            EntityType = "PrerequisiteFor",
            AttributeName = "ruleCode",
            DisplayName = "规则编号",
            DataType = EavDataTypes.String,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000024-0000-4000-8000-000000000024",
            EntityType = "PrerequisiteFor",
            AttributeName = "precondition",
            DisplayName = "前置条件",
            DataType = EavDataTypes.String,
            DisplayOrder = 1,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000025-0000-4000-8000-000000000025",
            EntityType = "DependsOn",
            AttributeName = "dependencyType",
            DisplayName = "依赖类型",
            DataType = EavDataTypes.String,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000026-0000-4000-8000-000000000026",
            EntityType = "Affects",
            AttributeName = "impactLevel",
            DisplayName = "影响等级",
            DataType = EavDataTypes.String,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000027-0000-4000-8000-000000000027",
            EntityType = "Affects",
            AttributeName = "estimatedRecoveryMinutes",
            DisplayName = "预计恢复时长",
            DataType = EavDataTypes.Int,
            DisplayOrder = 1,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000028-0000-4000-8000-000000000028",
            EntityType = "TrippedBy",
            AttributeName = "eventTime",
            DisplayName = "事件时刻",
            DataType = EavDataTypes.Datetime,
            DisplayOrder = 0,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000029-0000-4000-8000-000000000029",
            EntityType = "TrippedBy",
            AttributeName = "faultType",
            DisplayName = "故障类型",
            DataType = EavDataTypes.String,
            DisplayOrder = 1,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000030-0000-4000-8000-000000000030",
            EntityType = "LocatedAt",
            AttributeName = "room",
            DisplayName = "房间",
            DataType = EavDataTypes.String,
            DisplayOrder = 1,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000031-0000-4000-8000-000000000031",
            EntityType = "Protects",
            AttributeName = "settingUnit",
            DisplayName = "定值单位",
            DataType = EavDataTypes.String,
            DisplayOrder = 2,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        // ============ LineNode（跨越点）属性 ============
        new AttributeDefinition
        {
            AttributeId = "a0000032-0000-4000-8000-000000000032",
            EntityType = "LineNode",
            AttributeName = "clearance_m",
            DisplayName = "垂直净距（米）",
            DataType = EavDataTypes.Decimal,
            DisplayOrder = 1,
            IsRequired = true,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000033-0000-4000-8000-000000000033",
            EntityType = "LineNode",
            AttributeName = "crossing_angle",
            DisplayName = "交叉角（度）",
            DataType = EavDataTypes.Decimal,
            DisplayOrder = 2,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000034-0000-4000-8000-000000000034",
            EntityType = "LineNode",
            AttributeName = "crossing_type",
            DisplayName = "跨越物类型",
            DataType = EavDataTypes.SingleChoice,
            DisplayOrder = 3,
            IsRequired = true,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000035-0000-4000-8000-000000000035",
            EntityType = "LineNode",
            AttributeName = "voltage_below",
            DisplayName = "被跨线路电压等级",
            DataType = EavDataTypes.String,
            DisplayOrder = 4,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000036-0000-4000-8000-000000000036",
            EntityType = "LineNode",
            AttributeName = "location_geo",
            DisplayName = "经纬度",
            DataType = EavDataTypes.Location,
            DisplayOrder = 5,
            IsRequired = false,
            IsSearchable = false,
            IsSortable = false,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        },
        new AttributeDefinition
        {
            AttributeId = "a0000037-0000-4000-8000-000000000037",
            EntityType = "LineNode",
            AttributeName = "measured_at",
            DisplayName = "最近测量时间",
            DataType = EavDataTypes.Datetime,
            DisplayOrder = 6,
            IsRequired = false,
            IsSearchable = true,
            IsSortable = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        }
    ];
}

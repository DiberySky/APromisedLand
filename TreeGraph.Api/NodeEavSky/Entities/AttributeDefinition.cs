using System.Text.Json;

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
}

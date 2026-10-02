namespace TreeGraph.Api.Entities;

/// <summary>组合类型定义</summary>
public class CompositeTypeDefinition
{
    public long CompositeTypeId { get; set; }
    public string EntityType { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<CompositeFieldDefinition> Fields { get; set; } = new();
}

/// <summary>
/// 组合类型字段定义。
///
/// ★ #8：新增 RefOptionSetId，组合内 single_choice 可用选项集（与属性级对齐）。
/// 与 AllowedValues 并存：优先使用 RefOptionSetId，未设置时回退到 AllowedValues。
/// 数据库 CHECK 约束：仅 single_choice 类型可以设置 ref_option_set_id。
/// </summary>
public class CompositeFieldDefinition
{
    public long FieldId { get; set; }
    public long CompositeTypeId { get; set; }
    public string FieldName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DataType { get; set; } = "string";

    /// <summary>当 DataType = "composite" 时指向嵌套类型</summary>
    public long? RefCompositeTypeId { get; set; }
    public CompositeTypeDefinition? RefCompositeType { get; set; }

    /// <summary>数量字段的基准单位（仅 DataType = "decimal" 允许）</summary>
    public Guid? UnitId { get; set; }
    public Unit? Unit { get; set; }

    /// <summary>
    /// ★ #8：选项集引用（仅 DataType = "single_choice" 允许）。
    /// 设置后，选项集提供的 Value / Label 优先于 AllowedValues。
    /// </summary>
    public long? RefOptionSetId { get; set; }
    public OptionSet? RefOptionSet { get; set; }

    public bool IsArray { get; set; }
    public bool IsRequired { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsSortable { get; set; }
    public bool IsDeleted { get; set; }
    public int DisplayOrder { get; set; }

    public System.Text.Json.JsonDocument? ValidationRule { get; set; }
    public System.Text.Json.JsonDocument? AllowedValues { get; set; }
    public string? DefaultValue { get; set; }

    public CompositeTypeDefinition CompositeType { get; set; } = null!;
}

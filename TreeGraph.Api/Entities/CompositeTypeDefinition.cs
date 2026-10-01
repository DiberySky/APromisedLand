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

/// <summary>组合类型字段定义（DataType = "composite" 时可嵌套）</summary>
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

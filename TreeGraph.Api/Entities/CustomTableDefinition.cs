using System.Text.Json;

namespace TreeGraph.Api.Entities;

/// <summary>自定义表结构定义（1:N，多行结构；列结构由 CustomTableColumn 定义）</summary>
public class CustomTableDefinition
{
    public long TableDefinitionId { get; set; }
    public string EntityType { get; set; } = "";
    public string TableName { get; set; } = "";       // "certifications"、"education"
    public string DisplayName { get; set; } = "";     // "认证证书"、"教育经历"
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<CustomTableColumn> Columns { get; set; } = new();
}

/// <summary>自定义表列定义（任意基础类型或 composite）</summary>
public class CustomTableColumn
{
    public long ColumnId { get; set; }
    public long TableDefinitionId { get; set; }
    public string ColumnName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DataType { get; set; } = "string";

    /// <summary>当 DataType = "composite" 时指向嵌套组合类型</summary>
    public long? RefCompositeTypeId { get; set; }
    public CompositeTypeDefinition? RefCompositeType { get; set; }

    public bool IsRequired { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsSortable { get; set; }
    public bool IsUnique { get; set; }                // 列内唯一（如"证书名"不重复）
    public bool IsDeleted { get; set; }
    public int DisplayOrder { get; set; }
    public JsonDocument? ValidationRule { get; set; }
    public JsonDocument? AllowedValues { get; set; }
    public string? DefaultValue { get; set; }

    public CustomTableDefinition Table { get; set; } = null!;
}

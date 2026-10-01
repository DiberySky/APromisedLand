using System.Text.Json;

namespace TreeGraph.Shared.Eav.Dtos;

public class CreateAttributeRequest
{
    public string EntityType { get; set; } = "";
    public string AttributeName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DataType { get; set; } = "string";
    public bool IsRequired { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsSortable { get; set; }
    public bool IsMultiValue { get; set; }
    public int DisplayOrder { get; set; }
    public Guid? UnitId { get; set; }
    public long? RefCompositeTypeId { get; set; }
    public long? RefTableDefinitionId { get; set; }
    public long? RefOptionSetId { get; set; }
    public string? DefaultValue { get; set; }
    public JsonElement? AllowedValues { get; set; }
    public JsonElement? ValidationRule { get; set; }
}

public class CreateCompositeTypeRequest
{
    public string EntityType { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public class CreateCompositeFieldRequest
{
    public string FieldName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DataType { get; set; } = "string";
    public long? RefCompositeTypeId { get; set; }
    public bool IsArray { get; set; }
    public bool IsRequired { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsSortable { get; set; }
    public int DisplayOrder { get; set; }
    public string? DefaultValue { get; set; }
    public JsonElement? AllowedValues { get; set; }
    public JsonElement? ValidationRule { get; set; }
}

public class CreateCustomTableRequest
{
    public string EntityType { get; set; } = "";
    public string TableName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int DisplayOrder { get; set; }
}

public class CreateTableColumnRequest
{
    public string ColumnName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DataType { get; set; } = "string";
    public long? RefCompositeTypeId { get; set; }
    public bool IsRequired { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsSortable { get; set; }
    public bool IsUnique { get; set; }
    public int DisplayOrder { get; set; }
    public string? DefaultValue { get; set; }
    public JsonElement? AllowedValues { get; set; }
    public JsonElement? ValidationRule { get; set; }
}

public record CompositeTypeDetailDto(
    long CompositeTypeId,
    string EntityType,
    string TypeName,
    string DisplayName,
    int Version,
    IReadOnlyList<CompositeFieldDetailDto> Fields);

public record CompositeFieldDetailDto(
    long FieldId,
    string FieldName,
    string DisplayName,
    string DataType,
    long? RefCompositeTypeId,
    bool IsArray,
    bool IsRequired,
    bool IsSearchable,
    bool IsSortable,
    int DisplayOrder,
    string? DefaultValue,
    JsonElement? AllowedValues,
    JsonElement? ValidationRule);

public class UpdateCompositeTypeRequest
{
    public string? DisplayName { get; set; }
}

public class UpdateCompositeFieldRequest
{
    public string? DisplayName { get; set; }
    public bool? IsRequired { get; set; }
    public bool? IsSearchable { get; set; }
    public bool? IsSortable { get; set; }
    public int? DisplayOrder { get; set; }
    public string? DefaultValue { get; set; }
    public JsonElement? AllowedValues { get; set; }
    public JsonElement? ValidationRule { get; set; }
}

public record CustomTableDetailDto(
    long TableDefinitionId,
    string EntityType,
    string TableName,
    string DisplayName,
    int Version,
    int DisplayOrder,
    IReadOnlyList<CustomTableColumnDto> Columns);

public record CustomTableColumnDto(
    long ColumnId,
    string ColumnName,
    string DisplayName,
    string DataType,
    long? RefCompositeTypeId,
    bool IsRequired,
    bool IsSearchable,
    bool IsSortable,
    bool IsUnique,
    int DisplayOrder,
    string? DefaultValue,
    JsonElement? AllowedValues,
    JsonElement? ValidationRule);

public class UpdateCustomTableRequest
{
    public string? DisplayName { get; set; }
    public int? DisplayOrder { get; set; }
}

public class UpdateTableColumnRequest
{
    public string? DisplayName { get; set; }
    public bool? IsRequired { get; set; }
    public bool? IsSearchable { get; set; }
    public bool? IsSortable { get; set; }
    public bool? IsUnique { get; set; }
    public int? DisplayOrder { get; set; }
    public string? DefaultValue { get; set; }
    public JsonElement? AllowedValues { get; set; }
    public JsonElement? ValidationRule { get; set; }
}

public record AttributeDetailDto(
    long AttributeId,
    string EntityType,
    string AttributeName,
    string DisplayName,
    string DataType,
    bool IsRequired,
    bool IsSearchable,
    bool IsSortable,
    bool IsMultiValue,
    bool IsDeleted,
    int Version,
    int DisplayOrder,
    string? DefaultValue,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    JsonElement? AllowedValues,
    JsonElement? ValidationRule,
    Guid? UnitId,
    string? UnitName,
    string? UnitSymbol,
    string? UnitCategory,
    long? RefCompositeTypeId,
    string? CompositeTypeName,
    string? CompositeTypeDisplayName,
    long? RefTableDefinitionId,
    string? TableName,
    string? TableDisplayName,
    long? RefOptionSetId,
    string? OptionSetName,
    string? OptionSetDisplayName);

/// <summary>
/// 更新属性定义（★ 修复 P1-3：新增 Clear* 布尔，用于显式清除引用；
/// null 语义仍表示"不修改"）。
/// </summary>
public class UpdateAttributeRequest
{
    public string? DisplayName { get; set; }
    public bool? IsRequired { get; set; }
    public bool? IsSearchable { get; set; }
    public bool? IsSortable { get; set; }
    public bool? IsMultiValue { get; set; }
    public int? DisplayOrder { get; set; }
    public string? DefaultValue { get; set; }
    public JsonElement? AllowedValues { get; set; }
    public JsonElement? ValidationRule { get; set; }

    // 引用（按需修改；null 表示不动）
    public Guid? UnitId { get; set; }
    public long? RefCompositeTypeId { get; set; }
    public long? RefTableDefinitionId { get; set; }
    public long? RefOptionSetId { get; set; }

    // 显式清除（优先级高于上面的赋值字段）
    public bool ClearUnitId { get; set; }
    public bool ClearRefCompositeTypeId { get; set; }
    public bool ClearRefTableDefinitionId { get; set; }
    public bool ClearRefOptionSetId { get; set; }
}

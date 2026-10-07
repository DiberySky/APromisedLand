using System.Text.Json;

namespace TreeGraph.Shared.NodeEav.Dtos;

public class CreateAttributeRequest
{
    public string EntityType { get; set; } = "";

    /// <summary>
    /// 内部标识（JSON key）。可选：
    ///   - null / 空：服务端自动生成 `attr_` + 12 位 hex（如 attr_3f9a2b1c8d4e）
    ///   - 非空：必须以字母开头，只含字母、数字、下划线
    ///
    /// 提示：属性名会出现在 JSON key / 查询过滤 / 审计日志 / 导出列名里，
    /// 如需与外部系统对接，建议显式指定可读的英文标识（如 screen_size）。
    /// </summary>
    public string? AttributeName { get; set; }

    public string DisplayName { get; set; } = "";
    public string DataType { get; set; } = "string";
    public bool IsRequired { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsSortable { get; set; }
    public int DisplayOrder { get; set; }
    public Guid? UnitId { get; set; }
    public string? RefCompositeTypeId { get; set; }
    public string? RefTableDefinitionId { get; set; }
    public string? RefOptionSetId { get; set; }
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
    public string? RefCompositeTypeId { get; set; }
    public Guid? UnitId { get; set; }
    public string? RefOptionSetId { get; set; }
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
    public string? RefCompositeTypeId { get; set; }
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
    string CompositeTypeId,
    string EntityType,
    string TypeName,
    string DisplayName,
    int Version,
    IReadOnlyList<CompositeFieldDetailDto> Fields,
    bool IsDeleted = false);

public record CompositeFieldDetailDto(
    string FieldId,
    string FieldName,
    string DisplayName,
    string DataType,
    string? RefCompositeTypeId,
    bool IsArray,
    bool IsRequired,
    bool IsSearchable,
    bool IsSortable,
    int DisplayOrder,
    string? DefaultValue,
    JsonElement? AllowedValues,
    JsonElement? ValidationRule,
    Guid? UnitId = null,
    string? RefOptionSetId = null,
    string? OptionSetName = null,
    string? OptionSetDisplayName = null,
    bool IsDeleted = false);

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

    public string? RefOptionSetId { get; set; }
    public bool ClearRefOptionSetId { get; set; }

    public Guid? UnitId { get; set; }
    public bool ClearUnitId { get; set; }
}

public record CustomTableDetailDto(
    string TableDefinitionId,
    string EntityType,
    string TableName,
    string DisplayName,
    int Version,
    int DisplayOrder,
    IReadOnlyList<CustomTableColumnDto> Columns,
    bool IsDeleted = false);

public record CustomTableColumnDto(
    string ColumnId,
    string ColumnName,
    string DisplayName,
    string DataType,
    string? RefCompositeTypeId,
    bool IsRequired,
    bool IsSearchable,
    bool IsSortable,
    bool IsUnique,
    int DisplayOrder,
    string? DefaultValue,
    JsonElement? AllowedValues,
    JsonElement? ValidationRule,
    bool IsDeleted = false);

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
    string AttributeId,
    string EntityType,
    string AttributeName,
    string DisplayName,
    string DataType,
    bool IsRequired,
    bool IsSearchable,
    bool IsSortable,
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
    string? RefCompositeTypeId,
    string? CompositeTypeName,
    string? CompositeTypeDisplayName,
    string? RefTableDefinitionId,
    string? TableName,
    string? TableDisplayName,
    string? RefOptionSetId,
    string? OptionSetName,
    string? OptionSetDisplayName);

public class UpdateAttributeRequest
{
    public string? DisplayName { get; set; }
    public bool? IsRequired { get; set; }
    public bool? IsSearchable { get; set; }
    public bool? IsSortable { get; set; }
    public int? DisplayOrder { get; set; }
    public string? DefaultValue { get; set; }
    public JsonElement? AllowedValues { get; set; }
    public JsonElement? ValidationRule { get; set; }

    public Guid? UnitId { get; set; }
    public string? RefCompositeTypeId { get; set; }
    public string? RefTableDefinitionId { get; set; }
    public string? RefOptionSetId { get; set; }

    public bool ClearUnitId { get; set; }
    public bool ClearRefCompositeTypeId { get; set; }
    public bool ClearRefTableDefinitionId { get; set; }
    public bool ClearRefOptionSetId { get; set; }
}

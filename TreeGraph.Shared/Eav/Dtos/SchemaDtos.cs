using System.Text.Json;

namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>
/// 组合类型字段 Schema。
///
/// ★ #8：新增 OptionSet。
///   - 仅 DataType = "single_choice" 且绑定了选项集时非空
///   - 前端优先使用 OptionSet.Items（含 Value + Label）
///   - 未绑定时回退到 AllowedValues（仅 Value 列表）
/// </summary>
public sealed record CompositeFieldSchemaDto(
    string FieldName,
    string DisplayName,
    string DataType,
    bool IsArray,
    bool IsRequired,
    bool IsSearchable,
    int DisplayOrder,
    long? RefCompositeTypeId = null,
    CompositeTypeSchemaDto? NestedType = null,
    JsonElement? ValidationRule = null,
    JsonElement? AllowedValues = null,
    UnitSchemaDto? Unit = null,
    IReadOnlyList<UnitSchemaDto>? AvailableUnits = null,
    OptionSetSchemaDto? OptionSet = null);

/// <summary>组合类型 Schema</summary>
public sealed record CompositeTypeSchemaDto(
    string TypeName,
    IReadOnlyList<CompositeFieldSchemaDto> Fields);

/// <summary>计量单位 Schema</summary>
public sealed record UnitSchemaDto(
    Guid Id,
    string Category,
    string Name,
    string Symbol,
    bool IsBaseUnit);

/// <summary>选项项 Schema</summary>
public sealed record OptionItemSchemaDto(
    long OptionItemId,
    string Value,
    string Label,
    int DisplayOrder,
    bool IsDefault);

/// <summary>选项集 Schema</summary>
public sealed record OptionSetSchemaDto(
    long OptionSetId,
    string SetName,
    string DisplayName,
    IReadOnlyList<OptionItemSchemaDto> Items);

/// <summary>动态属性 Schema</summary>
public sealed record AttributeSchemaDto(
    string AttributeName,
    string DisplayName,
    string DataType,
    bool IsRequired,
    bool IsSearchable,
    bool IsSortable,
    int DisplayOrder,
    JsonElement? AllowedValues,
    JsonElement? ValidationRule,
    CompositeTypeSchemaDto? CompositeType,
    UnitSchemaDto? Unit = null,
    IReadOnlyList<UnitSchemaDto>? AvailableUnits = null,
    OptionSetSchemaDto? OptionSet = null,
    long? RefTableDefinitionId = null);

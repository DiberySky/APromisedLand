using System.Text.Json;

namespace TreeGraph.Shared.NodeEav.Dtos;

public sealed record CompositeFieldSchemaDto(
    string FieldName,
    string DisplayName,
    string DataType,
    bool IsArray,
    bool IsRequired,
    bool IsSearchable,
    int DisplayOrder,
    string? RefCompositeTypeId = null,
    CompositeTypeSchemaDto? NestedType = null,
    JsonElement? ValidationRule = null,
    JsonElement? AllowedValues = null,
    UnitSchemaDto? Unit = null,
    IReadOnlyList<UnitSchemaDto>? AvailableUnits = null,
    OptionSetSchemaDto? OptionSet = null);

public sealed record CompositeTypeSchemaDto(
    string TypeName,
    IReadOnlyList<CompositeFieldSchemaDto> Fields);

public sealed record UnitSchemaDto(
    Guid Id,
    string Category,
    string Name,
    string Symbol,
    bool IsBaseUnit);

public sealed record OptionItemSchemaDto(
    string OptionItemId,
    string Value,
    string Label,
    int DisplayOrder,
    bool IsDefault);

public sealed record OptionSetSchemaDto(
    string OptionSetId,
    string SetName,
    string DisplayName,
    IReadOnlyList<OptionItemSchemaDto> Items);

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
    string? RefTableDefinitionId = null);

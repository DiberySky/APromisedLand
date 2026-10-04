# TreeGraph.Shared C# 代码清单

- 生成时间：2026-10-04 21:37:23
- 文件总数：14
- 排除：bin/、obj/、csproj、README.md
- 项目状态：EAV 共享层（Dtos/Validators 等）；GUID String 主键形态

## 文件 1/14 TreeGraph.Shared/Eav/Dtos/AttributeFilter.cs

```csharp
namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>动态属性过滤条件</summary>
public class AttributeFilter
{
    public string AttributeName { get; set; } = "";

    /// <summary>组合类型字段路径，如 "address.city"</summary>
    public string? FieldPath { get; set; }

    /// <summary>eq / neq / gt / gte / lt / lte / between / in / like / startswith / endswith / near / bbox</summary>
    public string Operator { get; set; } = "eq";

    public object? Value { get; set; }

    /// <summary>between 的第二个值</summary>
    public object? Value2 { get; set; }
}
```

## 文件 2/14 TreeGraph.Shared/Eav/Dtos/CustomTableDtos.cs

```csharp
namespace TreeGraph.Shared.Eav.Dtos;

public class CustomTableValue
{
    public string TableName { get; set; } = "";
    public List<CustomTableRowValue> Rows { get; set; } = new();
}

public class CustomTableRowValue
{
    /// <summary>已有行有 GUID 字符串 Id；新行为 null。</summary>
    public string? RowId { get; set; }

    public int RowOrder { get; set; }
    public Dictionary<string, object?> Fields { get; set; } = new();
}
```

## 文件 3/14 TreeGraph.Shared/Eav/Dtos/EavQueryRequest.cs

```csharp
namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>EAV 动态查询请求</summary>
public class EavQueryRequest
{
    public string EntityType { get; set; } = "";

    public List<AttributeFilter> Filters { get; set; } = new();

    /// <summary>预留：按动态属性排序（当前按 EntityId 排序）</summary>
    public string? OrderByAttribute { get; set; }

    public bool OrderDescending { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
```

## 文件 4/14 TreeGraph.Shared/Eav/Dtos/EntityDtos.cs

```csharp
using System.Text.Json;

namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>
/// 动态实体（GET entities/{id} 及查询结果项）。
///
/// EntityId 是 GUID 字符串（36 字符）。
/// UpdatedAt 用于乐观锁（GET 拿、PUT/PATCH 回传）。
/// </summary>
public record DynamicEntityDto(
    string EntityId,
    string EntityType,
    Dictionary<string, JsonElement> Properties,
    DateTimeOffset? UpdatedAt = null);

/// <summary>审计历史项（GET entities/{id}/history）。</summary>
public record EntityHistoryDto(
    string AuditId,
    string EntityId,
    string EntityType,
    string AttributeId,
    string AttributeName,
    string? OldValue,
    string? NewValue,
    string ChangeType,
    string ChangedBy,
    DateTimeOffset ChangedAt,
    string? CorrelationId,
    string? ClientIp);

/// <summary>按自定义表行内数据查询父实体（POST entities/query-by-table）。</summary>
public class QueryByTableRequest
{
    public string AttributeName { get; set; } = "";
    public List<Dictionary<string, object?>> RowConditions { get; set; } = new();
}

/// <summary>批量删除请求（POST entities/batch-delete）。</summary>
public class BatchDeleteRequest
{
    /// <summary>待删除的实体 ID 列表（GUID 字符串）。重复会被去重。</summary>
    public List<string> EntityIds { get; set; } = new();
}

/// <summary>批量删除结果。</summary>
public record BatchDeleteResultDto(
    IReadOnlyList<string> Deleted,
    IReadOnlyList<string> NotFound,
    int TotalAttributesDeleted);
```

## 文件 5/14 TreeGraph.Shared/Eav/Dtos/EntityTypeDtos.cs

```csharp
using System.Text.Json;

namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>创建实体类型（POST api/eav/entity-types）</summary>
public class CreateEntityTypeRequest
{
    /// <summary>
    /// 可选。不传时服务端自动生成 `et_` + 12 位 hex（如 et_3f9a2b1c8d4e）。
    /// 保留字段用于脚本 / 工具导入时指定标识。
    /// </summary>
    public string? EntityType { get; set; }

    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>更新实体类型（PUT api/eav/entity-types/{id}）</summary>
public class UpdateEntityTypeRequest
{
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
}

/// <summary>实体类型详情（GET api/eav/entity-types/{id}）</summary>
public record EntityTypeDetailDto(
    string EntityTypeId,
    string EntityType,
    string DisplayName,
    string? Description,
    int DisplayOrder,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int AttributeCount);
```

## 文件 6/14 TreeGraph.Shared/Eav/Dtos/EntityTypeSummaryDto.cs

```csharp
namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>
/// 实体类型摘要（GET api/eav/entity-types）。
/// 用于侧边栏/首页列出所有已定义的实体类型。
/// </summary>
public record EntityTypeSummaryDto(
    string EntityType,
    int AttributeCount,
    int SearchableAttributeCount,
    string? FirstDisplayName,
    string? EntityTypeId = null,     // 新增
    string? DisplayName = null);     // 新增
```

## 文件 7/14 TreeGraph.Shared/Eav/Dtos/InodeDtos.cs

```csharp
using System.Text.Json;

namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>
/// iNode 下的类型卡片（UI 展示用）。
/// </summary>
public record InodeTypeCardDto(
    string EntityType,                    // 类型名，如 "Product"
    string DisplayName,                   // 中文显示名
    string? EntityTypeId,                 // 对应 entity_type_catalog 的 ID（可空）
    string? Description,
    bool Declared,                        // 是否已声明
    bool HasEntity,                       // 该类型下是否已创建实体
    DateTimeOffset? EntityUpdatedAt);

/// <summary>
/// iNode 下的实体概要（列表项）。
/// </summary>
public record InodeEntitySummaryDto(
    string InodeId,
    string EntityId,
    string EntityType,
    string? DisplayName,
    DateTimeOffset? UpdatedAt,
    int PropertyCount);

/// <summary>
/// 跨 iNode 查询请求。
/// </summary>
public class InodeQueryRequest
{
    /// <summary>可选：限定 iNode。null = 跨所有 iNode 查询。</summary>
    public string? InodeId { get; set; }

    /// <summary>必填：实体类型名（如 "Product"）。</summary>
    public string EntityType { get; set; } = "";

    public List<AttributeFilter> Filters { get; set; } = new();

    public string? OrderByAttribute { get; set; }
    public bool OrderDescending { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// 跨 iNode 查询结果项。
/// </summary>
public record InodeEntityDto(
    string InodeId,
    string EntityId,
    string EntityType,
    Dictionary<string, JsonElement> Properties,
    DateTimeOffset? UpdatedAt);
```

## 文件 8/14 TreeGraph.Shared/Eav/Dtos/MetadataDtos.cs

```csharp
using System.Text.Json;

namespace TreeGraph.Shared.Eav.Dtos;

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
```

## 文件 9/14 TreeGraph.Shared/Eav/Dtos/OptionSetDtos.cs

```csharp
namespace TreeGraph.Shared.Eav.Dtos;

public class CreateOptionSetRequest
{
    public string EntityType { get; set; } = "";
    public string SetName { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public record OptionSetSummaryDto(
    string OptionSetId,
    string EntityType,
    string SetName,
    string DisplayName,
    bool IsDeleted = false);

public class CreateOptionItemRequest
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdateOptionItemRequest
{
    public string? Label { get; set; }
    public int? DisplayOrder { get; set; }
    public bool? IsDefault { get; set; }
}

public class ReorderOptionItem
{
    public string OptionItemId { get; set; } = "";
    public int DisplayOrder { get; set; }
}

public record OptionSetDetailDto(
    string OptionSetId,
    string EntityType,
    string SetName,
    string DisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<OptionItemDetailDto> Items,
    bool IsDeleted = false);

public record OptionItemDetailDto(
    string OptionItemId,
    string Value,
    string Label,
    int DisplayOrder,
    bool IsDefault,
    bool IsDeleted,
    DateTimeOffset CreatedAt);

public record OptionSetReferenceDto(
    string AttributeId,
    string EntityType,
    string AttributeName,
    string DisplayName);

public class UpdateOptionSetRequest
{
    public string? DisplayName { get; set; }
}
```

## 文件 10/14 TreeGraph.Shared/Eav/Dtos/PagedResult.cs

```csharp
namespace TreeGraph.Shared.Eav.Dtos;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
```

## 文件 11/14 TreeGraph.Shared/Eav/Dtos/SchemaDtos.cs

```csharp
using System.Text.Json;

namespace TreeGraph.Shared.Eav.Dtos;

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
```

## 文件 12/14 TreeGraph.Shared/Eav/Dtos/UnitDtos.cs

```csharp
namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>单位（GET api/units、GET api/units/categories 内嵌项）</summary>
public record UnitDto(
    Guid Id,
    string Category,
    string Name,
    string Symbol,
    decimal ToBaseFactor,
    bool IsBaseUnit,
    int DisplayOrder);

/// <summary>单位分类分组视图（GET api/units/categories）</summary>
public record UnitCategoryDto(
    string Category,
    UnitDto? BaseUnit,
    IReadOnlyList<UnitDto> Units);

/// <summary>创建单位（POST api/units）</summary>
public class CreateUnitRequest
{
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public decimal ToBaseFactor { get; set; }
    public bool IsBaseUnit { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// 更新单位（PUT api/units/{id}）。
///
/// 可修改：Name / Symbol / DisplayOrder / IsBaseUnit。
///
/// 不可修改：
///   - Category：请使用 migrate-category 端点。
///   - ToBaseFactor：请使用 recalculate-factor 端点。
///
/// IsBaseUnit 语义：
///   - false → true：升级为基准，同分类其它单位自动降级。
///   - true → false：被拒绝（会导致分类失去基准）。
///   - null：不修改。
/// </summary>
public class UpdateUnitRequest
{
    public string? Name { get; set; }
    public string? Symbol { get; set; }
    public int? DisplayOrder { get; set; }
    public bool? IsBaseUnit { get; set; }
}

/// <summary>
/// 迁移单位到其它分类（POST api/units/{id}/migrate-category，保守策略）。
///
/// 单位被任何 AttributeDefinition.UnitId 绑定，或被 AttributeValue.UnitId
/// 引用为输入单位时，请求被拒绝——请先解除引用。
///
/// 迁移后 ToBaseFactor 语义变化，必须重设。
/// </summary>
public class MigrateUnitCategoryRequest
{
    public string NewCategory { get; set; } = "";
    public decimal NewToBaseFactor { get; set; }
}

/// <summary>
/// 修改换算系数并重算数据（POST api/units/{id}/recalculate-factor）。
///
/// 数据库端批量 UPDATE（同一事务 3 条 SQL）：
///   - 该单位作为 <b>输入单位</b> 引用（v.UnitId == id）的历史数据：
///     存储值 × newF / oldF（保持物理量不变）
///   - 该单位作为 <b>基准单位</b> 绑定（属性.UnitId == id）的历史数据：
///     存储值 × oldF / newF
///   - 同时命中两种角色的行：存储值不变
///
/// 预计影响行数超过 20 万时拒绝（阈值保护）。危险操作，建议先备份数据库。
/// </summary>
public class RecalculateUnitFactorRequest
{
    public decimal NewToBaseFactor { get; set; }
}

/// <summary>重算结果（POST api/units/{id}/recalculate-factor 响应）</summary>
public record RecalculateUnitFactorResult(
    int AffectedValues,
    int AffectedAttributes,
    decimal OldFactor,
    decimal NewFactor);
```

## 文件 13/14 TreeGraph.Shared/Eav/Dtos/ValidationModels.cs

```csharp
namespace TreeGraph.Shared.Eav.Dtos;

public record ValidationError(string Field, string Message);

public record ValidationResult(bool IsValid, List<ValidationError> Errors);
```

## 文件 14/14 TreeGraph.Shared/Eav/EavDataTypes.cs

```csharp
namespace TreeGraph.Shared.Eav;

/// <summary>EAV 基础数据类型常量</summary>
public static class EavDataTypes
{
    public const string String = "string";
    public const string Int = "int";
    public const string Decimal = "decimal";
    public const string Bool = "bool";
    public const string Datetime = "datetime";
    public const string Date = "date";
    public const string Time = "time";
    public const string File = "file";
    public const string Json = "json";
    public const string Composite = "composite";
    public const string Table = "table";
    public const string SingleChoice = "single_choice";

    public static readonly HashSet<string> All = new()
    {
        String, Int, Decimal, Bool, Datetime, Date, Time,
        File, Json, Composite, Table, SingleChoice
    };
}
```


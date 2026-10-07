namespace TreeGraph.Shared.NodeEav.Dtos;

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

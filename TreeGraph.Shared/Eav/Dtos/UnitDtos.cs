namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>单位（GET api/units）</summary>
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
    UnitCategoryBaseDto? BaseUnit,
    IReadOnlyList<UnitCategoryItemDto> Units);

public record UnitCategoryBaseDto(Guid Id, string Name, string Symbol);

public record UnitCategoryItemDto(Guid Id, string Name, string Symbol, bool IsBaseUnit);

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

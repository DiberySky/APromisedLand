namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

public class AttributeDefinitionUpdateDto
{
    public string Name { get; set; } = null!;
    public int? Lines { get; set; }
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public string? UnitId { get; set; }

    // ===== 列元信息（动态表列定义时可更新）=====
    // 注：ParentId 一般不可改（列不可换表），故不提供
    public int? Order { get; set; }
    public bool? IsRequired { get; set; }
    public string? DefaultValue { get; set; }
}
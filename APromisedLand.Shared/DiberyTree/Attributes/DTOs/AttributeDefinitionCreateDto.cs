using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

public class AttributeDefinitionCreateDto
{
    public string Name { get; set; } = null!;
    public AttributeTypeEnum AttributeType { get; set; }
    // public AttributeType AttributeType { get; set; } = new();
    public int? MaxLength { get; set; }
    public int? Lines { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public string? UnitId { get; set; }

    // ===== 动态表专用（创建列定义时指定）=====
    // ParentId 指向所属表定义；为 null 表示这是表定义本身
    public string? ParentId { get; set; }

    public bool HasDate { get; set; }
    public bool HasTime { get; set; }
    public bool HasRowNo { get; set; }
    public int Order { get; set; }
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
}
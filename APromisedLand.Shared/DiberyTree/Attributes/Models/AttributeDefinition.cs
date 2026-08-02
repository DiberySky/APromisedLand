using APromisedLand.Shared.Models;

namespace APromisedLand.Shared.DiberyTree.Attributes.Models;

public class AttributeDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = null!;

    public int AttributeTypeId { get; set; }
    public AttributeType AttributeType { get; set; } = null!;

    // 文本专用
    public int? Lines { get; set; }
    // 数字专用
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    // 单位（可选）
    public string? UnitOfMeasureId { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }

    // 导航集合
    public ICollection<TextAttributeValue> TextValues { get; set; } = new List<TextAttributeValue>();
    public ICollection<DecimalAttributeValue> DecimalValues { get; set; } = new List<DecimalAttributeValue>();
    public ICollection<IntegerAttributeValue> IntegerValues { get; set; } = new List<IntegerAttributeValue>();
    public ICollection<DateAttributeValue> DateValues { get; set; } = new List<DateAttributeValue>();
    public ICollection<TimeAttributeValue> TimeValues { get; set; } = new List<TimeAttributeValue>();
    public ICollection<DateTimeAttributeValue> DateTimeValues { get; set; } = new List<DateTimeAttributeValue>();
    public ICollection<FileAttributeValue> FileValues { get; set; } = new List<FileAttributeValue>();
    public ICollection<LocationAttributeValue> LocationValues { get; set; } = new List<LocationAttributeValue>();
}
using APromisedLand.Shared.DiberyTree.Attributes.Enums;

namespace APromisedLand.Shared.DiberyTree.Attributes.Models;

public class AttributeType
{
    public int Id { get; set; }
    public string Name { get; set; } = nameof(AttributeTypeEnum.文本);
    public string? Description { get; set; }
    public AttributeTypeEnum SystemType { get; set; } = AttributeTypeEnum.文本;

    public ICollection<AttributeDefinition> Definitions { get; set; } = new List<AttributeDefinition>();
}
using APromisedLand.Shared.DiberyTree.Attributes.Enums;

namespace APromisedLand.Shared.DiberyTree.Attributes.Models;

public class AttributeType
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public AttributeTypeEnum SystemType { get; set; }

    public ICollection<AttributeDefinition> Definitions { get; set; } = new List<AttributeDefinition>();
}
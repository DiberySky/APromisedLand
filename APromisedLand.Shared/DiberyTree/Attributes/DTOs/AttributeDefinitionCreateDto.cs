using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

public class AttributeDefinitionCreateDto
{
    public string Name { get; set; } = null!;
    public AttributeType AttributeType { get; set; } = null!;
    public int? Lines { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public string? UnitOfMeasureId { get; set; }
}
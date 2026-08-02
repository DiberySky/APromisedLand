namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

public class AttributeDefinitionUpdateDto
{
    public string Name { get; set; } = null!;
    public int? Lines { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public string? UnitOfMeasureId { get; set; }
}
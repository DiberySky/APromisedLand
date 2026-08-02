namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

public class AttributeDefinitionCreateDto
{
    public string Name { get; set; } = null!;
    public int AttributeTypeId { get; set; }
    public int? Lines { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public string? UnitOfMeasureId { get; set; }
}
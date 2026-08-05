using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

public class AttributeDefinitionDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public AttributeType AttributeType { get; set; } = new();
    public string AttributeTypeName { get; set; } = null!; // 便于前端展示
    public int? Lines { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public string? UnitOfMeasureId { get; set; }
    public string? UnitOfMeasureName { get; set; } 
}
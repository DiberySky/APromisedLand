namespace APromisedLand.Shared.DiberyTree.Attributes.Models;

public abstract class AttributeValueBase
{
    public int Id { get; set; }
    public string NodeId { get; set; } = null!;
    public string AttributeDefinitionId { get; set; } = null!; 
    public AttributeDefinition Definition { get; set; } = null!;
}
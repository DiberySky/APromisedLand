namespace APromisedLand.Shared.DiberyTree.Attributes.Models;

public abstract class AttributeValueBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString(); 
    public string NodeId { get; set; } = null!;
    public string AttributeDefinitionId { get; set; } = null!; 
    // public AttributeDefinition Definition { get; set; } = null!;
}
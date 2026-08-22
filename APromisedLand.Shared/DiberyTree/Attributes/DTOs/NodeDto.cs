namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

public class NodeDto
{
    public string Id { get; set; } = null!;
    public List<AttributeDto> AttributeDtos { get; set; } = new();
}
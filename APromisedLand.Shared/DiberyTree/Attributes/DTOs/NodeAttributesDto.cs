namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

public class NodeAttributesDto
{
    public string Id { get; set; } = null!;
    public List<AttributeJsonValueDto> AttributeJsonValueDtos { get; set; } = [];
}
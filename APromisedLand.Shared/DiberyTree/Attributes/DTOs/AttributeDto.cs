using System.Text.Json;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

public class AttributeDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DefinitionId { get; set; } = null!;
    public AttributeDefinition Definition { get; set; } = new();
    
    public JsonElement Value { get; set; }

}
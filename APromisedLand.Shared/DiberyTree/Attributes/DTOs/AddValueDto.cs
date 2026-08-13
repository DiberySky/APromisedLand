using System.Text.Json;

namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

public record AddValueDto(
    string AttributeDefinitionId,
    JsonElement Value
);
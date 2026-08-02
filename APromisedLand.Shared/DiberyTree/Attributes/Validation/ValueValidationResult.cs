using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.Shared.DiberyTree.Attributes.Validation;

public class ValueValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public AttributeValueBase? ValueEntity { get; set; }
}
namespace APromisedLand.Shared.DTOs.Units;

public class CreateUnitOfMeasureCommand
{
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string? Description { get; set; }
}
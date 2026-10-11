namespace TreeGraph.Blazor.Shared.NodeEavSky.Services;

public class NumericInput
{
    public decimal Value { get; set; }
    public string? UnitId { get; set; }

    public object ToSubmitValue()
        => UnitId is null ? Value : new { value = Value, unitId = UnitId };
}

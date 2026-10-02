namespace TreeGraph.Blazor.Services;

public class NumericInput
{
    public decimal Value { get; set; }
    public Guid? UnitId { get; set; }

    public object ToSubmitValue()
        => UnitId is null ? Value : new { value = Value, unitId = UnitId.Value };
}

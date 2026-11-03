namespace TreeGraph.Blazor.Shared.NodeEavSky.Services;

public class NumericInput
{
    public decimal Value { get; set; }

    public object ToSubmitValue() => Value;
}

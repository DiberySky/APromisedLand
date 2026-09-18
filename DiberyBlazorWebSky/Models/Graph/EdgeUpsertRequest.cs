namespace DiberyBlazorWebSky.Models.Graph;

/// <summary>与 MAFWorkFlowApi.Models.EdgeUpsertRequest 对应。</summary>
public record EdgeUpsertRequest
{
    public Guid? Guid { get; init; }
    public required Guid From { get; init; }
    public required Guid To { get; init; }
    public required string Type { get; init; }
    public bool Undirected { get; init; }
    public double? Cost { get; init; }
    public Dictionary<string, object?> Attributes { get; init; } = new();
}
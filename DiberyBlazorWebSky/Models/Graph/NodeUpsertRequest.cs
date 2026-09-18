namespace DiberyBlazorWebSky.Models.Graph;

/// <summary>与 MAFWorkFlowApi.Models.NodeUpsertRequest 对应。</summary>
public record NodeUpsertRequest
{
    public Guid? Guid { get; init; }
    public required string DisplayName { get; init; }
    public Guid? ParentGuid { get; init; }
    public required string Type { get; init; }
    public List<string>? ExtraLabels { get; init; }
    public Dictionary<string, object?> Attributes { get; init; } = new();
}
namespace DiberyBlazorWebSky.Models.Graph;

public record NodeUpsertResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public NodeDto? Node { get; init; }
    public bool IsNew { get; init; }
    public string? PathName { get; init; }
}
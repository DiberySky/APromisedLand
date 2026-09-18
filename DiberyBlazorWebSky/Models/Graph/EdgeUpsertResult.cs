namespace DiberyBlazorWebSky.Models.Graph;

public record EdgeUpsertResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public EdgeDto? Edge { get; init; }
    public bool IsNew { get; init; }
}
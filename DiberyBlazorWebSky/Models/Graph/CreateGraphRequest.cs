namespace DiberyBlazorWebSky.Models.Graph;

public record CreateGraphRequest
{
    public required string Name { get; init; }
    public Dictionary<string, object?>? Data { get; init; }
}
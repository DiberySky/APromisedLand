namespace DiberyBlazorWebSky.Models;

/// <summary>与 API 的 SessionMessage 对应。</summary>
public record SessionMessage
{
    public required string Role { get; init; }
    public required string Text { get; init; }
    public string? AuthorName { get; init; }
}
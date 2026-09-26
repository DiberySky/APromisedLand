namespace MafSampleApi.Models;

/// <summary>GET /api/sessions 的响应体。</summary>
public sealed record SessionListDto
{
    public IReadOnlyList<string> Ids { get; init; } = Array.Empty<string>();
}
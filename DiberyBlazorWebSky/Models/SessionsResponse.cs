namespace DiberyBlazorWebSky.Models;

/// <summary>与 API 的 SessionsReply 对应。</summary>
public record SessionsResponse
{
    /// <summary>会话 ID 列表。</summary>
    public required List<string> Sessions { get; init; }
}
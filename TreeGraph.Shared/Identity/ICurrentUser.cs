namespace TreeGraph.Shared.Identity;

/// <summary>
/// 当前请求用户的服务端视图。由 API / MafWfApi / Blazor 各自实现，
/// 接口位于契约层，实现不依赖任何第三方库。
/// </summary>
public interface ICurrentUser
{
    /// <summary>Keycloak 用户 sub。</summary>
    string UserId { get; }

    /// <summary>用户名（preferred_username）。</summary>
    string? UserName { get; }

    /// <summary>realm 角色。</summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>所属空间标识（来自 groups 中 <c>space-</c> 前缀项，保留前缀）。</summary>
    IReadOnlyList<string> SpaceIds { get; }

    /// <summary>是否具备指定角色。</summary>
    bool IsInRole(string role);
}

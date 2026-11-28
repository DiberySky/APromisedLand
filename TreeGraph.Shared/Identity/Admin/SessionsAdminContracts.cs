namespace TreeGraph.Shared.Identity.Admin;

/// <summary>用户会话。</summary>
public sealed record IdentitySession(
    string Id,
    string? UserId,
    string? Username,
    string? IpAddress,
    long? Start,
    long? LastAccess,
    IReadOnlyList<string> Clients,
    bool Offline);

/// <summary>会话管理服务。</summary>
public interface ISessionsAdminService
{
    Task<IReadOnlyList<IdentitySession>> ListUserSessionsAsync(string userId, CancellationToken ct = default);

    Task<AdminResult> LogoutSessionAsync(AdminActor actor, string userId, string sessionId, CancellationToken ct = default);

    Task<AdminResult> LogoutAllUserSessionsAsync(AdminActor actor, string userId, CancellationToken ct = default);
}

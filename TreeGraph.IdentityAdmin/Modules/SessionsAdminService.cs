using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>用户会话管理实现。</summary>
public sealed class SessionsAdminService : ISessionsAdminService
{
    private const string Module = "sessions";

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;
    private readonly IUsersAdminService _users;

    public SessionsAdminService(
        KeycloakAdminClient client,
        AdminOperationRunner runner,
        IUsersAdminService users)
    {
        _client = client;
        _runner = runner;
        _users = users;
    }

    public async Task<IReadOnlyList<IdentitySession>> ListUserSessionsAsync(
        string userId,
        CancellationToken ct = default)
    {
        var response = await _client
            .GetAsync($"users/{userId}/sessions", Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            return [];

        var sessions = response.Deserialize<List<KcUserSession>>() ?? [];
        return sessions.Select(s => new IdentitySession(
            s.Id ?? string.Empty,
            s.UserId,
            s.Username,
            s.IpAddress,
            s.Start,
            s.LastAccess,
            s.Clients?.Keys.ToArray() ?? [],
            false)).ToArray();
    }

    public async Task<AdminResult> LogoutSessionAsync(
        AdminActor actor,
        string userId,
        string sessionId,
        CancellationToken ct = default)
    {
        var target = await _users.GetAsync(userId, ct).ConfigureAwait(false);
        if (target is null)
            return AdminResult.Fail("用户不存在（404）", 404);
        var isSuperuser = string.Equals(
                              target.Username, IdentityConstants.SuperuserUsername,
                              StringComparison.OrdinalIgnoreCase)
                          || IdentityRoles.HasSuperuserRole(target.Roles);
        if (!actor.IsAdmin || (!actor.IsSuperuser
                && (isSuperuser || IdentityRoles.HasAdminRole(target.Roles))))
            return AdminResult.Fail("管理员不能管理其他管理员或超级用户的会话", 403);

        var sessions = await ListUserSessionsAsync(userId, ct).ConfigureAwait(false);
        if (!sessions.Any(s => s.Id == sessionId))
            return AdminResult.Fail("会话不存在或不属于该用户", 404);

        return await _runner.ExecuteAsync(
            actor,
            "session.logout",
            "session",
            sessionId,
            new { userId, sessionId },
            token => _client.SendAsync(HttpMethod.Delete, $"sessions/{sessionId}", null, Module, token),
            ct).ConfigureAwait(false);
    }

    public Task<AdminResult> LogoutAllUserSessionsAsync(
        AdminActor actor,
        string userId,
        CancellationToken ct = default)
        => _users.LogoutSessionsAsync(actor, userId, ct);
}

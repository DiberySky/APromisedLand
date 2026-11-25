using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>用户会话管理实现。</summary>
public sealed class SessionsAdminService : ISessionsAdminService
{
    private const string Module = "sessions";

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;

    public SessionsAdminService(KeycloakAdminClient client, AdminOperationRunner runner)
    {
        _client = client;
        _runner = runner;
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

    public Task<AdminResult> LogoutSessionAsync(AdminActor actor, string sessionId, CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "session.logout",
            "session",
            sessionId,
            new { sessionId },
            token => _client.SendAsync(HttpMethod.Delete, $"sessions/{sessionId}", null, Module, token),
            ct);

    public Task<AdminResult> LogoutAllUserSessionsAsync(
        AdminActor actor,
        string userId,
        CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "user.logout-sessions",
            "user",
            userId,
            new { userId },
            token => _client.SendAsync(HttpMethod.Post, $"users/{userId}/logout", null, Module, token),
            ct);
}

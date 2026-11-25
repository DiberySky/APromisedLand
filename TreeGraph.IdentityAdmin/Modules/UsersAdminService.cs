using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>用户管理实现。</summary>
public sealed class UsersAdminService : IUsersAdminService
{
    private const string Module = "users";

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;
    private readonly IIdentityRevocationStore _revocation;

    public UsersAdminService(
        KeycloakAdminClient client,
        AdminOperationRunner runner,
        IIdentityRevocationStore revocation)
    {
        _client = client;
        _runner = runner;
        _revocation = revocation;
    }

    public async Task<PagedResult<IdentityUserSummary>> SearchAsync(AdminPageRequest request, CancellationToken ct = default)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();

        var enabledParam = request.Enabled?.ToString().ToLowerInvariant();

        var query = KeycloakAdminClient.BuildQuery(
            ("search", search),
            ("first", request.First.ToString()),
            ("max", request.Max.ToString()),
            ("enabled", enabledParam),
            ("briefRepresentation", "true"));

        var listResponse = await _client.GetAsync("users" + query, Module, ct).ConfigureAwait(false);
        if (!listResponse.IsSuccess)
            return PagedResult<IdentityUserSummary>.Empty(request.NormalizedPage, request.NormalizedPageSize);

        var users = listResponse.Deserialize<List<KcUser>>() ?? [];

        var countQuery = KeycloakAdminClient.BuildQuery(("search", search), ("enabled", enabledParam));
        var countResponse = await _client.GetAsync("users/count" + countQuery, Module, ct).ConfigureAwait(false);
        var total = countResponse.IsSuccess && int.TryParse(countResponse.Body, out var parsed)
            ? parsed
            : users.Count;

        var items = users.Select(MapSummary).ToArray();
        return new PagedResult<IdentityUserSummary>(items, total, request.NormalizedPage, request.NormalizedPageSize);
    }

    public async Task<IdentityUserDetail?> GetAsync(string userId, CancellationToken ct = default)
    {
        var user = await _client.GetJsonAsync<KcUser>($"users/{userId}", Module, ct).ConfigureAwait(false);
        if (user is null)
            return null;

        var groupsResponse = await _client.GetAsync($"users/{userId}/groups", Module, ct).ConfigureAwait(false);
        var groups = groupsResponse.IsSuccess
            ? (groupsResponse.Deserialize<List<KcGroup>>() ?? []).Select(g => g.Path ?? g.Name ?? string.Empty).ToArray()
            : [];

        var rolesResponse = await _client.GetAsync($"users/{userId}/role-mappings/realm", Module, ct).ConfigureAwait(false);
        var roles = rolesResponse.IsSuccess
            ? (rolesResponse.Deserialize<List<KcRole>>() ?? [])
                .Select(r => r.Name ?? string.Empty)
                .Where(n => n.Length > 0)
                .ToArray()
            : [];

        return new IdentityUserDetail(
            user.Id ?? userId,
            user.Username ?? string.Empty,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Enabled ?? false,
            user.EmailVerified ?? false,
            user.CreatedTimestamp,
            groups,
            roles,
            user.RequiredActions ?? [],
            user.Attributes?.ToDictionary(
                p => p.Key,
                p => (IReadOnlyList<string>)p.Value.ToArray())
            ?? new Dictionary<string, IReadOnlyList<string>>());
    }

    public async Task<AdminResult<string>> CreateAsync(AdminActor actor, CreateUserRequest request, CancellationToken ct = default)
    {
        var body = new
        {
            username = request.Username,
            email = request.Email,
            firstName = request.FirstName,
            lastName = request.LastName,
            enabled = request.Enabled,
            emailVerified = request.EmailVerified,
            credentials = string.IsNullOrWhiteSpace(request.TemporaryPassword)
                ? null
                : new[]
                {
                    new
                    {
                        type = "password",
                        value = request.TemporaryPassword,
                        temporary = request.RequirePasswordChange
                    }
                },
            groups = request.GroupPaths,
            realmRoles = request.RealmRoles
        };

        return await _runner.ExecuteAsync<string>(
            actor,
            "user.create",
            "user",
            request.Username,
            request,
            async token =>
            {
                var response = await _client
                    .SendAsync(HttpMethod.Post, "users", body, Module, token)
                    .ConfigureAwait(false);
                return (response, response.ExtractCreatedId());
            },
            ct).ConfigureAwait(false);
    }

    public async Task<AdminResult> UpdateAsync(AdminActor actor, string userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        var current = await _client.GetJsonAsync<KcUser>($"users/{userId}", Module, ct).ConfigureAwait(false);
        if (current is null)
            return AdminResult.Fail("用户不存在（404）", 404);

        var body = new
        {
            id = current.Id ?? userId,
            username = current.Username,
            email = request.Email ?? current.Email,
            firstName = request.FirstName ?? current.FirstName,
            lastName = request.LastName ?? current.LastName,
            enabled = current.Enabled,
            emailVerified = request.EmailVerified ?? current.EmailVerified,
            requiredActions = current.RequiredActions,
            attributes = current.Attributes
        };

        return await _runner.ExecuteAsync(
            actor,
            "user.update",
            "user",
            userId,
            request,
            token => _client.SendAsync(HttpMethod.Put, $"users/{userId}", body, Module, token),
            ct).ConfigureAwait(false);
    }

    public Task<AdminResult> ResetPasswordAsync(
        AdminActor actor,
        string userId,
        ResetPasswordRequest request,
        CancellationToken ct = default)
    {
        var body = new
        {
            type = "password",
            value = request.Password,
            temporary = request.Temporary
        };

        return _runner.ExecuteAsync(
            actor,
            "user.reset-password",
            "user",
            userId,
            request,
            token => _client.SendAsync(HttpMethod.Put, $"users/{userId}/reset-password", body, Module, token),
            ct);
    }

    public async Task<AdminResult<DisableUserResult>> DisableAsync(
        AdminActor actor,
        string userId,
        CancellationToken ct = default)
    {
        var result = await _runner.ExecuteAsync<DisableUserResult>(
            actor,
            "user.disable",
            "user",
            userId,
            new { userId },
            async token =>
            {
                // 最后管理员保护（放在审计 Begin 之后，保持 fail-closed 语义）。
                if (await IsLastActiveAdminAsync(userId, token).ConfigureAwait(false))
                    return (new KeycloakResponse(409, string.Empty, null), null);

                var disableResponse = await _client
                    .SendAsync(HttpMethod.Put, $"users/{userId}", new { enabled = false }, Module, token)
                    .ConfigureAwait(false);

                if (!disableResponse.IsSuccess)
                    return (disableResponse, null);

                var sessionsRevoked = true;
                try
                {
                    var logout = await _client
                        .SendAsync(HttpMethod.Post, $"users/{userId}/logout", null, Module, token)
                        .ConfigureAwait(false);
                    sessionsRevoked = logout.IsSuccess;
                }
                catch (Exception)
                {
                    sessionsRevoked = false;
                }

                var revocationMarked = true;
                try
                {
                    await _revocation.MarkRevokedAsync(userId, DateTimeOffset.UtcNow, token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    revocationMarked = false;
                }

                var value = new DisableUserResult(
                    true,
                    sessionsRevoked,
                    revocationMarked,
                    !sessionsRevoked || !revocationMarked);

                return (disableResponse, value);
            },
            ct,
            value => value.SessionsRevoked && value.RevocationMarked ? "success" : "partial").ConfigureAwait(false);

        return result;
    }

    public async Task<AdminResult> EnableAsync(AdminActor actor, string userId, CancellationToken ct = default)
    {
        var result = await _runner.ExecuteAsync(
            actor,
            "user.enable",
            "user",
            userId,
            new { userId },
            token => _client.SendAsync(HttpMethod.Put, $"users/{userId}", new { enabled = true }, Module, token),
            ct).ConfigureAwait(false);

        if (result.Success)
        {
            try
            {
                await _revocation.ClearRevokedAsync(userId, ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 清除撤销标记失败不影响启用结果，下一次登录会自然过期。
            }
        }

        return result;
    }

    public Task<AdminResult> LogoutSessionsAsync(AdminActor actor, string userId, CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "user.logout-sessions",
            "user",
            userId,
            new { userId },
            token => _client.SendAsync(HttpMethod.Post, $"users/{userId}/logout", null, Module, token),
            ct);

    /// <summary>判断目标用户是否为唯一启用中的管理员；查询失败时不阻断（避免误锁）。</summary>
    private async Task<bool> IsLastActiveAdminAsync(string userId, CancellationToken ct)
    {
        var query = KeycloakAdminClient.BuildQuery(
            ("briefRepresentation", "true"),
            ("max", "200"));

        var response = await _client
            .GetAsync($"roles/{IdentityRoles.Admin}/users{query}", Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            return false;

        var users = response.Deserialize<List<KcUser>>() ?? [];
        var target = users.FirstOrDefault(u => string.Equals(u.Id, userId, StringComparison.Ordinal));
        if (target is null)
            return false;

        return users.Count(u => u.Enabled ?? true) <= 1;
    }

    private static IdentityUserSummary MapSummary(KcUser user)
        => new(
            user.Id ?? string.Empty,
            user.Username ?? string.Empty,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Enabled ?? false,
            user.EmailVerified ?? false,
            user.CreatedTimestamp);
}

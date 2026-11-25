using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>realm 角色管理实现。</summary>
public sealed class RolesAdminService : IRolesAdminService
{
    private const string Module = "roles";

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;

    public RolesAdminService(KeycloakAdminClient client, AdminOperationRunner runner)
    {
        _client = client;
        _runner = runner;
    }

    public async Task<IReadOnlyList<IdentityRole>> ListAsync(CancellationToken ct = default)
    {
        var response = await _client
            .GetAsync("roles?briefRepresentation=false", Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            return [];

        var roles = response.Deserialize<List<KcRole>>() ?? [];
        return roles
            .Where(r => r.ClientRole != true)
            .Select(Map)
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IdentityRole?> GetAsync(string roleName, CancellationToken ct = default)
    {
        var role = await _client
            .GetJsonAsync<KcRole>($"roles/{Uri.EscapeDataString(roleName)}", Module, ct)
            .ConfigureAwait(false);

        return role is null ? null : Map(role);
    }

    public Task<AdminResult<string>> CreateAsync(AdminActor actor, CreateRoleRequest request, CancellationToken ct = default)
    {
        var body = new
        {
            name = request.Name,
            description = request.Description,
            composite = request.Composite
        };

        return _runner.ExecuteAsync<string>(
            actor,
            "role.create",
            "role",
            request.Name,
            request,
            async token =>
            {
                var response = await _client
                    .SendAsync(HttpMethod.Post, "roles", body, Module, token)
                    .ConfigureAwait(false);
                return (response, request.Name);
            },
            ct);
    }

    public Task<AdminResult> UpdateAsync(
        AdminActor actor,
        string roleName,
        UpdateRoleRequest request,
        CancellationToken ct = default)
    {
        var body = new
        {
            name = request.NewName ?? roleName,
            description = request.Description
        };

        return _runner.ExecuteAsync(
            actor,
            "role.update",
            "role",
            roleName,
            request,
            token => _client.SendAsync(
                HttpMethod.Put,
                $"roles/{Uri.EscapeDataString(roleName)}",
                body,
                Module,
                token),
            ct);
    }

    public Task<AdminResult> DeleteAsync(AdminActor actor, string roleName, CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "role.delete",
            "role",
            roleName,
            new { roleName },
            token => _client.SendAsync(
                HttpMethod.Delete,
                $"roles/{Uri.EscapeDataString(roleName)}",
                null,
                Module,
                token),
            ct);

    public async Task<IReadOnlyList<IdentityUserSummary>> ListMembersAsync(
        string roleName,
        CancellationToken ct = default)
    {
        var response = await _client
            .GetAsync($"roles/{Uri.EscapeDataString(roleName)}/users?first=0&max=200", Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            return [];

        var users = response.Deserialize<List<KcUser>>() ?? [];
        return users.Select(u => new IdentityUserSummary(
            u.Id ?? string.Empty,
            u.Username ?? string.Empty,
            u.Email,
            u.FirstName,
            u.LastName,
            u.Enabled ?? false,
            u.EmailVerified ?? false,
            u.CreatedTimestamp)).ToArray();
    }

    public async Task<AdminResult> AddMemberAsync(
        AdminActor actor,
        string roleName,
        string userId,
        CancellationToken ct = default)
    {
        var role = await _client
            .GetAsync($"roles/{Uri.EscapeDataString(roleName)}", Module, ct)
            .ConfigureAwait(false);

        if (!role.IsSuccess)
            return KeycloakStatusMapper.ToResult(role.StatusCode);

        var representation = role.Deserialize<KcRole>();
        var body = new[]
        {
            new { id = representation?.Id, name = roleName }
        };

        return await _runner.ExecuteAsync(
            actor,
            "role.add-member",
            "role",
            roleName,
            new { roleName, userId },
            token => _client.SendAsync(
                HttpMethod.Post,
                $"users/{userId}/role-mappings/realm",
                body,
                Module,
                token),
            ct).ConfigureAwait(false);
    }

    public async Task<AdminResult> RemoveMemberAsync(
        AdminActor actor,
        string roleName,
        string userId,
        CancellationToken ct = default)
    {
        var role = await _client
            .GetAsync($"roles/{Uri.EscapeDataString(roleName)}", Module, ct)
            .ConfigureAwait(false);

        if (!role.IsSuccess)
            return KeycloakStatusMapper.ToResult(role.StatusCode);

        var representation = role.Deserialize<KcRole>();
        var body = new[]
        {
            new { id = representation?.Id, name = roleName }
        };

        return await _runner.ExecuteAsync(
            actor,
            "role.remove-member",
            "role",
            roleName,
            new { roleName, userId },
            token => _client.SendAsync(
                HttpMethod.Delete,
                $"users/{userId}/role-mappings/realm",
                body,
                Module,
                token),
            ct).ConfigureAwait(false);
    }

    private static IdentityRole Map(KcRole role)
        => new(
            role.Id ?? string.Empty,
            role.Name ?? string.Empty,
            role.Description,
            role.Composite ?? false);
}

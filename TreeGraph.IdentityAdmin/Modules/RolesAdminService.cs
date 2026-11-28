using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>realm 角色管理实现；系统角色和跨账号权限变更在服务端受控。</summary>
public sealed class RolesAdminService : IRolesAdminService
{
    private const string Module = "roles";

    private static readonly HashSet<string> SystemRoles = new(StringComparer.Ordinal)
    {
        IdentityRoles.Superuser,
        IdentityRoles.Admin,
        "app_admin",
        IdentityRoles.Editor,
        IdentityRoles.Viewer
    };

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;

    public RolesAdminService(KeycloakAdminClient client, AdminOperationRunner runner)
    {
        _client = client;
        _runner = runner;
    }

    public async Task<IReadOnlyList<IdentityRole>> ListAsync(CancellationToken ct = default)
    {
        var response = await _client.GetAsync("roles?briefRepresentation=false", Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return [];

        return (response.Deserialize<List<KcRole>>() ?? [])
            .Where(r => r.ClientRole != true)
            .Select(Map)
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IdentityRole?> GetAsync(string roleName, CancellationToken ct = default)
    {
        var role = await _client.GetJsonAsync<KcRole>(
            $"roles/{Uri.EscapeDataString(roleName)}", Module, ct).ConfigureAwait(false);
        return role is null ? null : Map(role);
    }

    public Task<AdminResult<string>> CreateAsync(AdminActor actor, CreateRoleRequest request, CancellationToken ct = default)
    {
        if (SystemRoles.Contains(request.Name))
            return Task.FromResult(AdminResult<string>.Fail("系统预置角色不可重复创建", 409));

        var body = new { name = request.Name, description = request.Description, composite = request.Composite };
        return _runner.ExecuteAsync<string>(
            actor,
            "role.create",
            "role",
            request.Name,
            request,
            async token =>
            {
                var response = await _client.SendAsync(HttpMethod.Post, "roles", body, Module, token)
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
        if (SystemRoles.Contains(roleName) || (request.NewName is not null && SystemRoles.Contains(request.NewName)))
            return Task.FromResult(AdminResult.Fail("系统预置角色不可修改或重命名", 409));

        var body = new { name = request.NewName ?? roleName, description = request.Description };
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
    {
        if (SystemRoles.Contains(roleName))
            return Task.FromResult(AdminResult.Fail("系统预置角色不可删除", 409));

        return _runner.ExecuteAsync(
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
    }

    public async Task<IReadOnlyList<IdentityUserSummary>> ListMembersAsync(
        string roleName,
        CancellationToken ct = default)
    {
        var response = await _client.GetAsync(
            $"roles/{Uri.EscapeDataString(roleName)}/users?first=0&max=200&briefRepresentation=false",
            Module,
            ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return [];

        return (response.Deserialize<List<KcUser>>() ?? [])
            .Select(u => new IdentityUserSummary(
                u.Id ?? string.Empty,
                u.Username ?? string.Empty,
                GetAttribute(u, "realName"),
                GetAttribute(u, "phoneNumber"),
                u.Enabled ?? false,
                u.CreatedTimestamp))
            .ToArray();
    }

    public async Task<AdminResult> AddMemberAsync(
        AdminActor actor,
        string roleName,
        string userId,
        CancellationToken ct = default)
    {
        if (roleName == IdentityRoles.Superuser)
            return AdminResult.Fail("超级用户只能由系统初始化预置", 403);

        var (target, error) = await GetTargetAsync(actor, userId, ct).ConfigureAwait(false);
        if (error is not null)
            return error;
        if (target is null)
            return AdminResult.Fail("用户不存在（404）", 404);

        var normalizedRole = roleName == "app_admin" ? IdentityRoles.Admin : roleName;
        if (normalizedRole == IdentityRoles.Admin && !actor.IsSuperuser)
            return AdminResult.Fail("管理员不能授予管理员角色", 403);
        if (normalizedRole == IdentityRoles.Admin && IsMainlandPhone(target.User.Username ?? string.Empty))
            return AdminResult.Fail("管理员账号必须使用自定义用户名，请通过新建管理员入口创建", 409);

        var roleResponse = await _client.GetAsync(
            $"roles/{Uri.EscapeDataString(roleName)}", Module, ct).ConfigureAwait(false);
        if (!roleResponse.IsSuccess)
            return KeycloakStatusMapper.ToResult(roleResponse.StatusCode);
        var representation = roleResponse.Deserialize<KcRole>();
        var body = new[] { new { id = representation?.Id, name = roleName } };

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
        var (target, error) = await GetTargetAsync(actor, userId, ct).ConfigureAwait(false);
        if (error is not null)
            return error;
        if (target is null)
            return AdminResult.Fail("用户不存在（404）", 404);

        var normalizedRole = roleName == "app_admin" ? IdentityRoles.Admin : roleName;
        if (normalizedRole == IdentityRoles.Superuser)
            return AdminResult.Fail("唯一超级用户角色不可通过产品界面移除", 409);
        if (normalizedRole == IdentityRoles.Admin && !actor.IsSuperuser)
            return AdminResult.Fail("管理员不能移除管理员角色", 403);
        if (target.IsSuperuser && actor.Sub == userId && normalizedRole == IdentityRoles.Admin)
            return AdminResult.Fail("超级用户不可自行降级", 409);

        var roleResponse = await _client.GetAsync(
            $"roles/{Uri.EscapeDataString(roleName)}", Module, ct).ConfigureAwait(false);
        if (!roleResponse.IsSuccess)
            return KeycloakStatusMapper.ToResult(roleResponse.StatusCode);
        var representation = roleResponse.Deserialize<KcRole>();
        var body = new[] { new { id = representation?.Id, name = roleName } };

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

    private async Task<(TargetUser? Target, AdminResult? Error)> GetTargetAsync(
        AdminActor actor,
        string userId,
        CancellationToken ct)
    {
        if (!actor.IsAdmin)
            return (null, AdminResult.Fail("无权分配角色", 403));

        var user = await _client.GetJsonAsync<KcUser>($"users/{userId}", Module, ct).ConfigureAwait(false);
        if (user is null)
            return (null, null);
        var rolesResponse = await _client.GetAsync($"users/{userId}/role-mappings/realm", Module, ct)
            .ConfigureAwait(false);
        if (!rolesResponse.IsSuccess)
            return (null, KeycloakStatusMapper.ToResult(rolesResponse.StatusCode));

        var roles = (rolesResponse.Deserialize<List<KcRole>>() ?? [])
            .Select(r => r.Name ?? string.Empty)
            .Where(n => n.Length > 0)
            .ToArray();
        var target = new TargetUser(user, roles);
        if (!actor.IsSuperuser && (target.IsSuperuser || IdentityRoles.HasAdminRole(roles)))
            return (null, AdminResult.Fail("管理员不能变更管理员或超级用户的角色", 403));
        return (target, null);
    }

    private static bool IsMainlandPhone(string username)
        => System.Text.RegularExpressions.Regex.IsMatch(
            username,
            "^1[3-9][0-9]{9}$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    private static string? GetAttribute(KcUser user, string name)
        => user.Attributes is not null
           && user.Attributes.TryGetValue(name, out var values)
           && values.Count > 0
            ? values[0]
            : null;

    private static IdentityRole Map(KcRole role)
        => new(
            role.Id ?? string.Empty,
            role.Name ?? string.Empty,
            role.Description,
            role.Composite ?? false);

    private sealed record TargetUser(KcUser User, IReadOnlyList<string> Roles)
    {
        public bool IsSuperuser
            => string.Equals(User.Username, IdentityConstants.SuperuserUsername, StringComparison.OrdinalIgnoreCase)
               || IdentityRoles.HasSuperuserRole(Roles);
    }
}

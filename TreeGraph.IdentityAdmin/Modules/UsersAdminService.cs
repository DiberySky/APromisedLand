using System.Text.RegularExpressions;
using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>用户管理实现；账号类型与目标用户权限均在服务端强制校验。</summary>
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
            ("briefRepresentation", "false"));

        var listResponse = await _client.GetAsync("users" + query, Module, ct).ConfigureAwait(false);
        if (!listResponse.IsSuccess)
            return PagedResult<IdentityUserSummary>.Empty(request.NormalizedPage, request.NormalizedPageSize);

        var users = listResponse.Deserialize<List<KcUser>>() ?? [];
        var countQuery = KeycloakAdminClient.BuildQuery(("search", search), ("enabled", enabledParam));
        var countResponse = await _client.GetAsync("users/count" + countQuery, Module, ct).ConfigureAwait(false);
        var total = countResponse.IsSuccess && int.TryParse(countResponse.Body, out var parsed)
            ? parsed
            : users.Count;

        return new PagedResult<IdentityUserSummary>(
            users.Select(MapSummary).ToArray(),
            total,
            request.NormalizedPage,
            request.NormalizedPageSize);
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
            GetAttribute(user, "realName"),
            GetAttribute(user, "phoneNumber"),
            user.Enabled ?? false,
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
        if (!actor.IsAdmin)
            return AdminResult<string>.Fail("无权创建用户", 403);

        var validation = ValidateCreateRequest(request);
        if (validation is not null)
            return AdminResult<string>.Fail(validation, 400);

        var roles = NormalizeRequestedRoles(request.RealmRoles);
        var isAdminAccount = IdentityRoles.HasAdminRole(roles);
        if (isAdminAccount && !actor.IsSuperuser)
            return AdminResult<string>.Fail("只有超级用户可以创建管理员", 403);
        if (IdentityRoles.HasSuperuserRole(roles))
            return AdminResult<string>.Fail("超级用户只能由系统初始化预置", 403);

        var username = request.Username.Trim();
        var realName = request.RealName.Trim();
        var attributes = new Dictionary<string, string[]> { ["realName"] = [realName] };
        if (!isAdminAccount)
            attributes["phoneNumber"] = [username];

        var body = new
        {
            username,
            enabled = true,
            attributes,
            groups = new[] { IdentityConstants.DefaultGroupPath },
            requiredActions = request.RequirePasswordChange ? new[] { "UPDATE_PASSWORD" } : [],
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = request.TemporaryPassword,
                    temporary = request.RequirePasswordChange
                }
            }
        };

        return await _runner.ExecuteAsync<string>(
            actor,
            "user.create",
            "user",
            username,
            request,
            async token =>
            {
                var response = await _client.SendAsync(HttpMethod.Post, "users", body, Module, token)
                    .ConfigureAwait(false);
                if (!response.IsSuccess)
                    return (response, null);

                var userId = response.ExtractCreatedId();
                if (string.IsNullOrEmpty(userId))
                {
                    var query = KeycloakAdminClient.BuildQuery(
                        ("username", username), ("exact", "true"));
                    var createdUsers = await _client.GetAsync("users" + query, Module, token).ConfigureAwait(false);
                    userId = createdUsers.IsSuccess
                        ? (createdUsers.Deserialize<List<KcUser>>() ?? [])
                            .FirstOrDefault(u => u.Username == username)?.Id
                        : null;
                }
                if (string.IsNullOrEmpty(userId))
                    return (new KeycloakResponse(502, "创建成功但未取得用户 ID", null), null);

                var assign = await AssignRealmRolesAsync(userId, roles, token).ConfigureAwait(false);
                return assign.IsSuccess
                    ? (response, userId)
                    : (assign, null);
            },
            ct).ConfigureAwait(false);
    }

    public async Task<AdminResult> UpdateAsync(AdminActor actor, string userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        var (target, error) = await GetManageableTargetAsync(actor, userId, ct).ConfigureAwait(false);
        if (error is not null)
            return error;
        if (target is null)
            return AdminResult.Fail("用户不存在（404）", 404);

        var realName = request.RealName?.Trim();
        if (string.IsNullOrWhiteSpace(realName) || realName.Length > 50)
            return AdminResult.Fail("真实姓名必填且不能超过 50 个字符", 400);

        var current = target.User;
        var attributes = current.Attributes?.ToDictionary(p => p.Key, p => p.Value)
                         ?? new Dictionary<string, List<string>>();
        attributes["realName"] = [realName];
        var body = new
        {
            id = current.Id ?? userId,
            username = current.Username,
            email = (string?)null,
            firstName = (string?)null,
            lastName = (string?)null,
            enabled = current.Enabled,
            emailVerified = false,
            requiredActions = current.RequiredActions,
            attributes
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

    public async Task<AdminResult> ResetPasswordAsync(
        AdminActor actor,
        string userId,
        ResetPasswordRequest request,
        CancellationToken ct = default)
    {
        var (target, error) = await GetManageableTargetAsync(actor, userId, ct).ConfigureAwait(false);
        if (error is not null)
            return error;
        if (target is null)
            return AdminResult.Fail("用户不存在（404）", 404);
        if (string.IsNullOrWhiteSpace(request.Password))
            return AdminResult.Fail("新密码不能为空", 400);

        var body = new { type = "password", value = request.Password, temporary = request.Temporary };
        return await _runner.ExecuteAsync(
            actor,
            "user.reset-password",
            "user",
            userId,
            request,
            async token =>
            {
                var reset = await _client.SendAsync(
                    HttpMethod.Put, $"users/{userId}/reset-password", body, Module, token).ConfigureAwait(false);
                if (!reset.IsSuccess)
                    return reset;

                var logout = await _client.SendAsync(
                    HttpMethod.Post, $"users/{userId}/logout", null, Module, token).ConfigureAwait(false);
                if (!logout.IsSuccess)
                    return logout;

                try
                {
                    await _revocation.MarkRevokedAsync(userId, DateTimeOffset.UtcNow, token).ConfigureAwait(false);
                    return reset;
                }
                catch (Exception)
                {
                    return new KeycloakResponse(503, "密码已重置，但旧令牌撤销标记写入失败", null);
                }
            },
            ct).ConfigureAwait(false);
    }

    public async Task<AdminResult<DisableUserResult>> DisableAsync(
        AdminActor actor,
        string userId,
        CancellationToken ct = default)
    {
        var (target, error) = await GetManageableTargetAsync(actor, userId, ct).ConfigureAwait(false);
        if (error is not null)
            return AdminResult<DisableUserResult>.Fail(error.Error ?? "无权操作", error.StatusCode);
        if (target is null)
            return AdminResult<DisableUserResult>.Fail("用户不存在（404）", 404);
        if (target.IsSuperuser)
            return AdminResult<DisableUserResult>.Fail("超级用户不可通过产品界面禁用", 409);

        return await _runner.ExecuteAsync<DisableUserResult>(
            actor,
            "user.disable",
            "user",
            userId,
            new { userId },
            async token =>
            {
                var disableResponse = await _client.SendAsync(
                    HttpMethod.Put, $"users/{userId}", new { enabled = false }, Module, token).ConfigureAwait(false);
                if (!disableResponse.IsSuccess)
                    return (disableResponse, null);

                var sessionsRevoked = true;
                try
                {
                    var logout = await _client.SendAsync(
                        HttpMethod.Post, $"users/{userId}/logout", null, Module, token).ConfigureAwait(false);
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
    }

    public async Task<AdminResult> EnableAsync(AdminActor actor, string userId, CancellationToken ct = default)
    {
        var (target, error) = await GetManageableTargetAsync(actor, userId, ct).ConfigureAwait(false);
        if (error is not null)
            return error;
        if (target is null)
            return AdminResult.Fail("用户不存在（404）", 404);

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
                // 保留原行为：启用成功不因缓存清理失败而回滚。
            }
        }

        return result;
    }

    public async Task<AdminResult> LogoutSessionsAsync(AdminActor actor, string userId, CancellationToken ct = default)
    {
        var (target, error) = await GetManageableTargetAsync(actor, userId, ct).ConfigureAwait(false);
        if (error is not null)
            return error;
        if (target is null)
            return AdminResult.Fail("用户不存在（404）", 404);

        return await _runner.ExecuteAsync(
            actor,
            "user.logout-sessions",
            "user",
            userId,
            new { userId },
            async token =>
            {
                var logout = await _client.SendAsync(
                    HttpMethod.Post, $"users/{userId}/logout", null, Module, token).ConfigureAwait(false);
                if (!logout.IsSuccess)
                    return logout;

                try
                {
                    await _revocation.MarkRevokedAsync(userId, DateTimeOffset.UtcNow, token).ConfigureAwait(false);
                    return logout;
                }
                catch (Exception)
                {
                    return new KeycloakResponse(503, "会话已注销，但旧令牌撤销标记写入失败", null);
                }
            },
            ct).ConfigureAwait(false);
    }

    private static string? ValidateCreateRequest(CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RealName) || request.RealName.Trim().Length > 50)
            return "真实姓名必填且不能超过 50 个字符";
        if (string.IsNullOrWhiteSpace(request.TemporaryPassword))
            return "初始密码不能为空";

        var roles = NormalizeRequestedRoles(request.RealmRoles);
        if (roles.Any(r => r is not (IdentityRoles.Superuser or IdentityRoles.Admin or "app_admin"
                or IdentityRoles.Editor or IdentityRoles.Viewer)))
            return "只允许分配系统预置角色";

        var isAdminAccount = IdentityRoles.HasAdminRole(roles) || IdentityRoles.HasSuperuserRole(roles);
        var username = request.Username?.Trim() ?? string.Empty;
        if (isAdminAccount)
        {
            if (!Regex.IsMatch(
                    username,
                    "^[A-Za-z][A-Za-z0-9._-]{2,63}$",
                    RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(100)))
                return "管理员用户名需为 3-64 位字母、数字、点、下划线或短横线，且以字母开头";
        }
        else if (!IsMainlandPhone(username))
        {
            return "普通用户登录名必须为中国大陆 11 位手机号";
        }

        return null;
    }

    private static string[] NormalizeRequestedRoles(IReadOnlyList<string>? roles)
    {
        if (roles is null || roles.Count == 0)
            return [IdentityRoles.Viewer];
        return roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Select(r => r == "app_admin" ? IdentityRoles.Admin : r)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsMainlandPhone(string username)
        => Regex.IsMatch(
            username,
            "^1[3-9][0-9]{9}$",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    private async Task<KeycloakResponse> AssignRealmRolesAsync(
        string userId,
        IReadOnlyList<string> roleNames,
        CancellationToken ct)
    {
        var mappings = new List<object>();
        foreach (var roleName in roleNames)
        {
            var roleResponse = await _client.GetAsync(
                $"roles/{Uri.EscapeDataString(roleName)}", Module, ct).ConfigureAwait(false);
            if (!roleResponse.IsSuccess)
                return roleResponse;
            var role = roleResponse.Deserialize<KcRole>();
            if (role?.Id is null)
                return new KeycloakResponse(404, "角色不存在", null);
            mappings.Add(new { id = role.Id, name = role.Name });
        }

        return await _client.SendAsync(
            HttpMethod.Post,
            $"users/{userId}/role-mappings/realm",
            mappings,
            Module,
            ct).ConfigureAwait(false);
    }

    private async Task<(TargetUser? Target, AdminResult? Error)> GetManageableTargetAsync(
        AdminActor actor,
        string userId,
        CancellationToken ct)
    {
        if (!actor.IsAdmin)
            return (null, AdminResult.Fail("无权管理用户", 403));

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
            return (null, AdminResult.Fail("管理员只能管理普通用户，不能管理管理员或超级用户", 403));

        return (target, null);
    }

    private static IdentityUserSummary MapSummary(KcUser user)
        => new(
            user.Id ?? string.Empty,
            user.Username ?? string.Empty,
            GetAttribute(user, "realName"),
            GetAttribute(user, "phoneNumber"),
            user.Enabled ?? false,
            user.CreatedTimestamp);

    private static string? GetAttribute(KcUser user, string name)
        => user.Attributes is not null
           && user.Attributes.TryGetValue(name, out var values)
           && values.Count > 0
            ? values[0]
            : null;

    private sealed record TargetUser(KcUser User, IReadOnlyList<string> Roles)
    {
        public bool IsSuperuser
            => string.Equals(User.Username, IdentityConstants.SuperuserUsername, StringComparison.OrdinalIgnoreCase)
               || IdentityRoles.HasSuperuserRole(Roles);
    }
}

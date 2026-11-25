using System.Text.Json.Nodes;
using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>客户端管理实现。</summary>
public sealed class ClientsAdminService : IClientsAdminService
{
    private const string Module = "clients";

    private static readonly HashSet<string> BuiltInClientIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "account",
        "admin-cli",
        "broker",
        "realm-management",
        "security-admin-console"
    };

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;

    public ClientsAdminService(KeycloakAdminClient client, AdminOperationRunner runner)
    {
        _client = client;
        _runner = runner;
    }

    public async Task<PagedResult<IdentityClientSummary>> SearchAsync(AdminPageRequest request, CancellationToken ct = default)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var query = KeycloakAdminClient.BuildQuery(
            ("clientId", search),
            ("search", search is null ? null : "true"),
            ("first", request.First.ToString()),
            ("max", request.Max.ToString()));

        var response = await _client.GetAsync("clients" + query, Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return PagedResult<IdentityClientSummary>.Empty(request.NormalizedPage, request.NormalizedPageSize);

        var clients = response.Deserialize<List<KcClient>>() ?? [];
        var items = clients.Select(c => new IdentityClientSummary(
            c.Id ?? string.Empty,
            c.ClientId ?? string.Empty,
            c.Name,
            c.Description,
            c.Enabled ?? false,
            c.PublicClient ?? false,
            c.Protocol)).ToArray();

        var total = clients.Count < request.Max
            ? request.First + clients.Count
            : request.First + clients.Count + 1;

        return new PagedResult<IdentityClientSummary>(items, total, request.NormalizedPage, request.NormalizedPageSize);
    }

    public async Task<IdentityClientDetail?> GetAsync(string clientId, CancellationToken ct = default)
    {
        var client = await _client.GetJsonAsync<KcClient>($"clients/{clientId}", Module, ct).ConfigureAwait(false);
        if (client is null)
            return null;

        return new IdentityClientDetail(
            client.Id ?? clientId,
            client.ClientId ?? string.Empty,
            client.Name,
            client.Description,
            client.Enabled ?? false,
            client.PublicClient ?? false,
            client.Protocol,
            client.ClientAuthenticatorType ?? "client-secret",
            client.RedirectUris ?? [],
            client.WebOrigins ?? [],
            client.StandardFlowEnabled ?? false,
            client.ImplicitFlowEnabled ?? false,
            client.DirectAccessGrantsEnabled ?? false,
            client.ServiceAccountsEnabled ?? false,
            client.RootUrl,
            client.BaseUrl,
            client.FullScopeAllowed ?? false,
            client.BearerOnly ?? false,
            client.DefaultClientScopes ?? [],
            client.OptionalClientScopes ?? []);
    }

    public Task<AdminResult<string>> CreateAsync(
        AdminActor actor,
        CreateClientRequest request,
        CancellationToken ct = default)
    {
        var body = new
        {
            clientId = request.ClientId,
            name = request.Name,
            description = request.Description,
            enabled = true,
            protocol = "openid-connect",
            publicClient = request.PublicClient,
            clientAuthenticatorType = request.PublicClient ? null : "client-secret",
            rootUrl = request.RootUrl,
            baseUrl = request.BaseUrl,
            redirectUris = request.RedirectUris ?? [],
            webOrigins = request.WebOrigins ?? [],
            standardFlowEnabled = request.StandardFlowEnabled,
            implicitFlowEnabled = false,
            directAccessGrantsEnabled = request.DirectAccessGrantsEnabled,
            serviceAccountsEnabled = request.ServiceAccountsEnabled,
            fullScopeAllowed = false
        };

        return _runner.ExecuteAsync<string>(
            actor,
            "client.create",
            "client",
            request.ClientId,
            request,
            async token =>
            {
                var response = await _client
                    .SendAsync(HttpMethod.Post, "clients", body, Module, token)
                    .ConfigureAwait(false);
                return (response, response.ExtractCreatedId());
            },
            ct);
    }

    public async Task<AdminResult> UpdateAsync(
        AdminActor actor,
        string clientId,
        UpdateClientRequest request,
        CancellationToken ct = default)
    {
        var current = await _client.GetAsync($"clients/{clientId}", Module, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return KeycloakStatusMapper.ToResult(current.StatusCode);

        var node = JsonNode.Parse(current.Body)?.AsObject();
        if (node is null)
            return AdminResult.Fail("客户端数据解析失败", 502);

        SetIfNotNull(node, "name", request.Name);
        SetIfNotNull(node, "description", request.Description);
        SetIfNotNull(node, "enabled", request.Enabled);
        SetIfNotNull(node, "rootUrl", request.RootUrl);
        SetIfNotNull(node, "baseUrl", request.BaseUrl);
        SetIfNotNull(node, "fullScopeAllowed", request.FullScopeAllowed);
        SetIfNotNull(node, "standardFlowEnabled", request.StandardFlowEnabled);
        SetIfNotNull(node, "implicitFlowEnabled", request.ImplicitFlowEnabled);
        SetIfNotNull(node, "directAccessGrantsEnabled", request.DirectAccessGrantsEnabled);
        SetIfNotNull(node, "serviceAccountsEnabled", request.ServiceAccountsEnabled);

        if (request.RedirectUris is not null)
            node["redirectUris"] = ToArray(request.RedirectUris);

        if (request.WebOrigins is not null)
            node["webOrigins"] = ToArray(request.WebOrigins);

        return await _runner.ExecuteAsync(
            actor,
            "client.update",
            "client",
            clientId,
            request,
            token => _client.SendAsync(HttpMethod.Put, $"clients/{clientId}", node, Module, token),
            ct).ConfigureAwait(false);
    }

    public async Task<AdminResult> DeleteAsync(AdminActor actor, string clientId, CancellationToken ct = default)
    {
        var current = await _client.GetJsonAsync<KcClient>($"clients/{clientId}", Module, ct).ConfigureAwait(false);
        if (current?.ClientId is not null && BuiltInClientIds.Contains(current.ClientId))
            return AdminResult.Fail("内置客户端不允许删除", 409);

        return await _runner.ExecuteAsync(
            actor,
            "client.delete",
            "client",
            clientId,
            new { clientId },
            token => _client.SendAsync(HttpMethod.Delete, $"clients/{clientId}", null, Module, token),
            ct).ConfigureAwait(false);
    }

    public async Task<AdminResult<ClientSecretResult>> GetSecretAsync(string clientId, CancellationToken ct = default)
    {
        var response = await _client.GetAsync($"clients/{clientId}/client-secret", Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return AdminResult<ClientSecretResult>.Fail(KeycloakStatusMapper.ToMessage(response.StatusCode), response.StatusCode);

        var secret = response.Deserialize<ClientSecretPayload>();
        return AdminResult<ClientSecretResult>.Ok(
            new ClientSecretResult(clientId, secret?.Value ?? string.Empty));
    }

    public Task<AdminResult<ClientSecretResult>> RegenerateSecretAsync(
        AdminActor actor,
        string clientId,
        CancellationToken ct = default)
        => _runner.ExecuteAsync<ClientSecretResult>(
            actor,
            "client.regenerate-secret",
            "client",
            clientId,
            new { clientId },
            async token =>
            {
                var response = await _client
                    .SendAsync(HttpMethod.Post, $"clients/{clientId}/client-secret", null, Module, token)
                    .ConfigureAwait(false);

                var secret = response.Deserialize<ClientSecretPayload>();
                return (response, new ClientSecretResult(clientId, secret?.Value ?? string.Empty));
            },
            ct);

    public async Task<AdminResult<ClientServiceAccountRoles>> GetServiceAccountRolesAsync(
        string clientId,
        CancellationToken ct = default)
    {
        var userId = await ResolveServiceAccountUserIdAsync(clientId, ct).ConfigureAwait(false);
        if (userId is null)
            return AdminResult<ClientServiceAccountRoles>.Fail("该客户端未启用 service account", 409);

        var roles = await _client
            .GetAsync($"users/{userId}/role-mappings/realm", Module, ct)
            .ConfigureAwait(false);

        var names = roles.IsSuccess
            ? (roles.Deserialize<List<KcRole>>() ?? [])
                .Select(r => r.Name ?? string.Empty)
                .Where(n => n.Length > 0)
                .ToArray()
            : [];

        return AdminResult<ClientServiceAccountRoles>.Ok(new ClientServiceAccountRoles(clientId, names));
    }

    public async Task<AdminResult> AddServiceAccountRoleAsync(
        AdminActor actor,
        string clientId,
        string roleName,
        CancellationToken ct = default)
    {
        var userId = await ResolveServiceAccountUserIdAsync(clientId, ct).ConfigureAwait(false);
        if (userId is null)
            return AdminResult.Fail("该客户端未启用 service account", 409);

        var role = await _client
            .GetAsync($"roles/{Uri.EscapeDataString(roleName)}", Module, ct)
            .ConfigureAwait(false);
        if (!role.IsSuccess)
            return KeycloakStatusMapper.ToResult(role.StatusCode);

        var representation = role.Deserialize<KcRole>();
        var body = new[] { new { id = representation?.Id, name = roleName } };

        return await _runner.ExecuteAsync(
            actor,
            "client.add-service-role",
            "client",
            clientId,
            new { clientId, roleName },
            token => _client.SendAsync(HttpMethod.Post, $"users/{userId}/role-mappings/realm", body, Module, token),
            ct).ConfigureAwait(false);
    }

    public async Task<AdminResult> RemoveServiceAccountRoleAsync(
        AdminActor actor,
        string clientId,
        string roleName,
        CancellationToken ct = default)
    {
        var userId = await ResolveServiceAccountUserIdAsync(clientId, ct).ConfigureAwait(false);
        if (userId is null)
            return AdminResult.Fail("该客户端未启用 service account", 409);

        var role = await _client
            .GetAsync($"roles/{Uri.EscapeDataString(roleName)}", Module, ct)
            .ConfigureAwait(false);
        if (!role.IsSuccess)
            return KeycloakStatusMapper.ToResult(role.StatusCode);

        var representation = role.Deserialize<KcRole>();
        var body = new[] { new { id = representation?.Id, name = roleName } };

        return await _runner.ExecuteAsync(
            actor,
            "client.remove-service-role",
            "client",
            clientId,
            new { clientId, roleName },
            token => _client.SendAsync(HttpMethod.Delete, $"users/{userId}/role-mappings/realm", body, Module, token),
            ct).ConfigureAwait(false);
    }

    private async Task<string?> ResolveServiceAccountUserIdAsync(string clientId, CancellationToken ct)
    {
        var response = await _client
            .GetAsync($"clients/{clientId}/service-account-user", Module, ct)
            .ConfigureAwait(false);

        return response.IsSuccess
            ? response.Deserialize<KcUser>()?.Id
            : null;
    }

    private static void SetIfNotNull(JsonObject node, string property, string? value)
    {
        if (value is not null)
            node[property] = value;
    }

    private static void SetIfNotNull(JsonObject node, string property, bool? value)
    {
        if (value is not null)
            node[property] = value.Value;
    }

    private static JsonArray ToArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
            array.Add(value);

        return array;
    }

    private sealed class ClientSecretPayload
    {
        public string? Type { get; set; }
        public string? Value { get; set; }
    }
}

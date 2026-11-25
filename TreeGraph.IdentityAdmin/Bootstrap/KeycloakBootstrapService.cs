using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.IdentityAdmin.Options;
using TreeGraph.IdentityAdmin.Token;
using TreeGraph.Shared.Identity;

namespace TreeGraph.IdentityAdmin.Bootstrap;

/// <summary>
/// 启动时通过 Admin API 幂等应用 realm 配置。
/// 不依赖 <c>--import-realm</c>（导入不会更新已存在的 realm）。
/// </summary>
public sealed class KeycloakBootstrapService : IHostedService
{
    private const string Module = "bootstrap";

    private static readonly (string Name, string Description)[] RealmRoles =
    [
        (IdentityRoles.Admin, "管理台入口与高危操作"),
        (IdentityRoles.Editor, "业务写权限"),
        (IdentityRoles.Viewer, "业务只读")
    ];

    private static readonly string[] RealmManagementRoles =
    [
        "view-realm", "manage-realm", "view-users", "query-users", "manage-users",
        "view-clients", "manage-clients", "view-identity-providers",
        "manage-identity-providers", "view-events", "manage-events", "query-groups"
    ];

    private readonly KeycloakAdminClient _client;
    private readonly IOptionsMonitor<KeycloakAdminOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<KeycloakBootstrapService> _logger;

    public KeycloakBootstrapService(
        KeycloakAdminClient client,
        IOptionsMonitor<KeycloakAdminOptions> options,
        TimeProvider timeProvider,
        ILogger<KeycloakBootstrapService> logger)
    {
        _client = client;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keycloak 引导配置失败；管理功能可能不可用");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task RunAsync(CancellationToken ct = default)
    {
        var options = _options.CurrentValue;
        _logger.LogInformation("开始应用 Keycloak realm 引导配置：realm={Realm}", _client.Realm);

        await EnsureRealmSettingsAsync(options, ct).ConfigureAwait(false);
        await EnsureRolesAsync(ct).ConfigureAwait(false);
        await EnsureGroupsAsync(ct).ConfigureAwait(false);

        var webClientId = await EnsureClientAsync(
                options.WebClientId,
                publicClient: false,
                standardFlow: true,
                serviceAccount: false,
                redirectUris: [options.WebBaseUrl.TrimEnd('/') + "/signin-oidc"],
                webOrigins: [options.WebBaseUrl.TrimEnd('/')],
                rootUrl: options.WebBaseUrl.TrimEnd('/'),
                ct)
            .ConfigureAwait(false);

        var mauiClientId = await EnsureClientAsync(
                "treegraph-maui",
                publicClient: true,
                standardFlow: true,
                serviceAccount: false,
                redirectUris: [options.MauiRedirectUri],
                webOrigins: ["+"],
                rootUrl: null,
                ct)
            .ConfigureAwait(false);

        var wasmBaseUrl = options.WasmBaseUrl.TrimEnd('/');
        var wasmClientId = await EnsureClientAsync(
                options.WasmClientId,
                publicClient: true,
                standardFlow: true,
                serviceAccount: false,
                redirectUris: [wasmBaseUrl + "/authentication/login-callback"],
                webOrigins: [wasmBaseUrl],
                rootUrl: wasmBaseUrl,
                ct,
                pkceOnly: true,
                postLogoutRedirectUri: wasmBaseUrl + "/",
                updateExisting: true,
                fullScopeAllowed: true)
            .ConfigureAwait(false);

        var adminClientId = await EnsureClientAsync(
                options.AdminClientId,
                publicClient: false,
                standardFlow: false,
                serviceAccount: true,
                redirectUris: [],
                webOrigins: [],
                rootUrl: null,
                ct)
            .ConfigureAwait(false);

        await SetClientSecretIfConfiguredAsync(webClientId, options.WebClientSecret, ct).ConfigureAwait(false);
        await SetClientSecretIfConfiguredAsync(adminClientId, options.AdminClientSecret, ct).ConfigureAwait(false);

        await EnsureTokenMappersAsync(webClientId, options.WebClientId, options.WebClientId, ct).ConfigureAwait(false);
        // 保持 MAUI 既有 audience 行为不变；WASM 使用独立 audience。
        await EnsureTokenMappersAsync(mauiClientId, "treegraph-maui", options.WebClientId, ct).ConfigureAwait(false);
        await EnsureTokenMappersAsync(wasmClientId, options.WasmClientId, options.WasmClientId, ct).ConfigureAwait(false);

        await AssignRealmManagementRolesAsync(adminClientId, ct).ConfigureAwait(false);
        await EnsureKcAdminApiScopeAsync(adminClientId, ct).ConfigureAwait(false);

        _logger.LogInformation("Keycloak realm 引导配置完成：realm={Realm}", _client.Realm);
    }

    private async Task EnsureRealmSettingsAsync(KeycloakAdminOptions options, CancellationToken ct)
    {
        var current = await _client.GetAsync(string.Empty, Module, ct).ConfigureAwait(false);
        ThrowIfUnauthorized(current, "读取 realm");
        if (!current.IsSuccess)
        {
            _logger.LogWarning("读取 realm 失败：HTTP {Status}", current.StatusCode);
            return;
        }

        var node = JsonNode.Parse(current.Body)?.AsObject();
        if (node is null)
            return;

        node["registrationAllowed"] = false;
        node["bruteForceProtected"] = true;
        node["passwordPolicy"] =
            "length(12) and upperCase(1) and lowerCase(1) and digits(1) and specialChars(1)";
        node["accessTokenLifespan"] = 600;
        node["ssoSessionIdleTimeout"] = 1800;
        node["ssoSessionMaxLifespan"] = 28800;

        var defaultGroups = new JsonArray();
        defaultGroups.Add(IdentityConstants.DefaultGroupPath);
        node["defaultGroups"] = defaultGroups;

        var response = await _client
            .SendAsync(HttpMethod.Put, string.Empty, node, Module, ct)
            .ConfigureAwait(false);

        ThrowIfUnauthorized(response, "更新 realm 设置");
        if (!response.IsSuccess)
            _logger.LogWarning("更新 realm 设置失败：HTTP {Status}", response.StatusCode);
    }

    private async Task EnsureRolesAsync(CancellationToken ct)
    {
        foreach (var (name, description) in RealmRoles)
        {
            var existing = await _client
                .GetAsync($"roles/{Uri.EscapeDataString(name)}", Module, ct)
                .ConfigureAwait(false);

            ThrowIfUnauthorized(existing, $"读取角色 {name}");
            if (existing.IsSuccess)
                continue;

            var response = await _client
                .SendAsync(
                    HttpMethod.Post,
                    "roles",
                    new { name, description, composite = false },
                    Module,
                    ct)
                .ConfigureAwait(false);

            ThrowIfUnauthorized(response, $"创建角色 {name}");
            if (!response.IsSuccess)
                _logger.LogWarning("创建角色失败：{Role} HTTP {Status}", name, response.StatusCode);
        }
    }

    private async Task EnsureGroupsAsync(CancellationToken ct)
    {
        await EnsureTopLevelGroupAsync("Admin", ct).ConfigureAwait(false);
        await EnsureTopLevelGroupAsync("diberysky", ct).ConfigureAwait(false);
        await EnsureTopLevelGroupAsync("spaces", ct).ConfigureAwait(false);
    }

    private async Task<string?> EnsureTopLevelGroupAsync(string name, CancellationToken ct)
    {
        var query = KeycloakAdminClient.BuildQuery(
            ("search", name),
            ("briefRepresentation", "false"),
            ("first", "0"),
            ("max", "200"));

        var list = await _client.GetAsync("groups" + query, Module, ct).ConfigureAwait(false);
        ThrowIfUnauthorized(list, $"查询组 {name}");
        if (list.IsSuccess)
        {
            var groups = list.Deserialize<List<KcGroup>>() ?? [];
            var existing = groups.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.Ordinal));
            if (existing is not null)
                return existing.Id;
        }

        var response = await _client
            .SendAsync(HttpMethod.Post, "groups", new { name }, Module, ct)
            .ConfigureAwait(false);

        ThrowIfUnauthorized(response, $"创建组 {name}");
        if (!response.IsSuccess)
        {
            _logger.LogWarning("创建组失败：{Group} HTTP {Status}", name, response.StatusCode);
            return null;
        }

        return response.ExtractCreatedId();
    }

    private async Task<string> EnsureClientAsync(
        string clientId,
        bool publicClient,
        bool standardFlow,
        bool serviceAccount,
        string[] redirectUris,
        string[] webOrigins,
        string? rootUrl,
        CancellationToken ct,
        bool pkceOnly = false,
        string? postLogoutRedirectUri = null,
        bool updateExisting = false,
        bool fullScopeAllowed = false)
    {
        var attributes = new Dictionary<string, string>();
        if (pkceOnly) attributes["pkce.code.challenge.method"] = "S256";
        if (!string.IsNullOrWhiteSpace(postLogoutRedirectUri))
            attributes["post.logout.redirect.uris"] = postLogoutRedirectUri;

        var body = new
        {
            clientId,
            name = clientId,
            enabled = true,
            protocol = "openid-connect",
            publicClient,
            clientAuthenticatorType = publicClient ? null : "client-secret",
            standardFlowEnabled = standardFlow,
            implicitFlowEnabled = false,
            directAccessGrantsEnabled = false,
            serviceAccountsEnabled = serviceAccount,
            redirectUris,
            webOrigins,
            rootUrl,
            baseUrl = rootUrl,
            fullScopeAllowed,
            attributes
        };

        var existing = await FindClientUuidAsync(clientId, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!updateExisting) return existing;
            var updated = await _client
                .SendAsync(HttpMethod.Put, $"clients/{existing}", body, Module, ct)
                .ConfigureAwait(false);
            if (!updated.IsSuccess)
                _logger.LogWarning("更新客户端失败：{Client} HTTP {Status}", clientId, updated.StatusCode);
            return existing;
        }

        var response = await _client
            .SendAsync(HttpMethod.Post, "clients", body, Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            _logger.LogWarning("创建客户端失败：{Client} HTTP {Status}", clientId, response.StatusCode);
            return string.Empty;
        }

        var createdUuid = response.ExtractCreatedId();
        if (string.IsNullOrEmpty(createdUuid))
            createdUuid = await FindClientUuidAsync(clientId, ct).ConfigureAwait(false) ?? string.Empty;

        return createdUuid;
    }

    private async Task<string?> FindClientUuidAsync(string clientId, CancellationToken ct)
    {
        var query = KeycloakAdminClient.BuildQuery(("clientId", clientId), ("first", "0"), ("max", "10"));
        var response = await _client.GetAsync("clients" + query, Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return null;

        var clients = response.Deserialize<List<KcClient>>() ?? [];
        return clients.FirstOrDefault(c => string.Equals(c.ClientId, clientId, StringComparison.Ordinal))?.Id;
    }

    private async Task SetClientSecretIfConfiguredAsync(string clientUuid, string secret, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(clientUuid) || string.IsNullOrEmpty(secret))
            return;

        var current = await _client.GetAsync($"clients/{clientUuid}", Module, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return;

        var node = JsonNode.Parse(current.Body)?.AsObject();
        if (node is null)
            return;

        node["secret"] = secret;

        var response = await _client
            .SendAsync(HttpMethod.Put, $"clients/{clientUuid}", node, Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            _logger.LogWarning("设置客户端密钥失败：{ClientId} HTTP {Status}", clientUuid, response.StatusCode);
    }

    private async Task EnsureTokenMappersAsync(string clientUuid, string clientId, string audience, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(clientUuid))
            return;

        await EnsureClientMapperAsync(
            clientUuid,
            "realm roles to roles claim",
            "oidc-usermodel-realm-role-mapper",
            new Dictionary<string, string>
            {
                ["claim.name"] = IdentityClaimTypes.Roles,
                ["jsonType.label"] = "String",
                ["multivalued"] = "true",
                ["access.token.claim"] = "true",
                ["id.token.claim"] = "false",
                ["userinfo.token.claim"] = "true",
                ["introspection.token.claim"] = "true"
            },
            ct).ConfigureAwait(false);

        await EnsureClientMapperAsync(
            clientUuid,
            "groups to groups claim",
            "oidc-group-membership-mapper",
            new Dictionary<string, string>
            {
                ["claim.name"] = IdentityClaimTypes.Groups,
                ["full.path"] = "false",
                ["access.token.claim"] = "true",
                ["id.token.claim"] = "false",
                ["userinfo.token.claim"] = "true",
                ["introspection.token.claim"] = "true"
            },
            ct).ConfigureAwait(false);

        await EnsureClientMapperAsync(
            clientUuid,
            $"{audience} audience",
            "oidc-audience-mapper",
            new Dictionary<string, string>
            {
                ["included.custom.audience"] = audience,
                ["access.token.claim"] = "true",
                ["id.token.claim"] = "false",
                ["introspection.token.claim"] = "true"
            },
            ct).ConfigureAwait(false);

        _logger.LogDebug("客户端 {ClientId} 令牌映射器已就绪", clientId);
    }

    private async Task EnsureClientMapperAsync(
        string clientUuid,
        string name,
        string protocolMapper,
        Dictionary<string, string> config,
        CancellationToken ct)
    {
        var list = await _client
            .GetAsync($"clients/{clientUuid}/protocol-mappers/models", Module, ct)
            .ConfigureAwait(false);

        JsonObject? existing = null;
        if (list.IsSuccess)
        {
            var mappers = JsonNode.Parse(list.Body)?.AsArray();
            if (mappers is not null)
            {
                foreach (var item in mappers)
                {
                    if (item is JsonObject obj
                        && string.Equals((string?)obj["name"], name, StringComparison.Ordinal))
                    {
                        existing = obj;
                        break;
                    }
                }
            }
        }

        var configNode = new JsonObject();
        foreach (var (key, value) in config)
            configNode[key] = value;

        if (existing is not null)
        {
            existing["config"] = configNode;
            await _client
                .SendAsync(
                    HttpMethod.Put,
                    $"clients/{clientUuid}/protocol-mappers/models/{(string?)existing["id"]}",
                    existing,
                    Module,
                    ct)
                .ConfigureAwait(false);
            return;
        }

        await _client
            .SendAsync(
                HttpMethod.Post,
                $"clients/{clientUuid}/protocol-mappers/models",
                new
                {
                    name,
                    protocol = "openid-connect",
                    protocolMapper,
                    config
                },
                Module,
                ct)
            .ConfigureAwait(false);
    }

    private async Task AssignRealmManagementRolesAsync(string adminClientUuid, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(adminClientUuid))
            return;

        var realmManagementUuid = await FindClientUuidAsync("realm-management", ct).ConfigureAwait(false);
        if (realmManagementUuid is null)
        {
            _logger.LogWarning("未找到 realm-management 客户端，跳过 service account 角色分配");
            return;
        }

        var serviceAccount = await _client
            .GetAsync($"clients/{adminClientUuid}/service-account-user", Module, ct)
            .ConfigureAwait(false);

        if (!serviceAccount.IsSuccess)
        {
            ThrowIfUnauthorized(serviceAccount, "读取 service account");
            _logger.LogWarning("读取 service account 失败：HTTP {Status}", serviceAccount.StatusCode);
            return;
        }

        var userId = serviceAccount.Deserialize<KcUser>()?.Id;
        if (string.IsNullOrEmpty(userId))
            return;

        var availableRoles = await _client
            .GetAsync($"clients/{realmManagementUuid}/roles", Module, ct)
            .ConfigureAwait(false);

        ThrowIfUnauthorized(availableRoles, "读取 realm-management 角色");
        if (!availableRoles.IsSuccess)
            return;

        var roles = availableRoles.Deserialize<List<KcRole>>() ?? [];
        var desired = roles
            .Where(r => r.Name is not null && RealmManagementRoles.Contains(r.Name, StringComparer.Ordinal))
            .Select(r => new { id = r.Id, name = r.Name })
            .ToArray();

        if (desired.Length == 0)
            return;

        var assign = await _client
            .SendAsync(
                HttpMethod.Post,
                $"users/{userId}/role-mappings/clients/{realmManagementUuid}",
                desired,
                Module,
                ct)
            .ConfigureAwait(false);

        ThrowIfUnauthorized(assign, "分配 realm-management 角色");
        if (!assign.IsSuccess)
            _logger.LogWarning("分配 realm-management 角色失败：HTTP {Status}", assign.StatusCode);
    }

    private async Task EnsureKcAdminApiScopeAsync(string adminClientUuid, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(adminClientUuid))
            return;

        var scopes = await _client.GetAsync("client-scopes", Module, ct).ConfigureAwait(false);
        ThrowIfUnauthorized(scopes, "读取 client scopes");
        if (!scopes.IsSuccess)
            return;

        var scope = (scopes.Deserialize<List<KcClientScope>>() ?? [])
            .FirstOrDefault(s => string.Equals(s.Name, "kc-admin-api", StringComparison.Ordinal));

        if (scope?.Id is null)
        {
            _logger.LogWarning("未找到 kc-admin-api client scope；管理令牌可能缺少 Admin API audience");
            return;
        }

        var response = await _client
            .SendAsync(
                HttpMethod.Put,
                $"clients/{adminClientUuid}/default-client-scopes/{scope.Id}",
                null,
                Module,
                ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            _logger.LogWarning("分配 kc-admin-api scope 失败：HTTP {Status}", response.StatusCode);
    }

    /// <summary>
    /// 401/403 说明服务账号缺少 realm-management 权限。bootstrap 自身无法提权，
    /// 必须显式失败，避免各步骤静默返回后仍打印“引导配置完成”。
    /// </summary>
    private static void ThrowIfUnauthorized(Keycloak.KeycloakResponse response, string step)
    {
        if (response.StatusCode is 401 or 403)
            throw new KeycloakUnauthorizedException(
                $"{step}失败：HTTP {response.StatusCode}，服务账号缺少 realm-management 权限");
    }
}

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

    private static readonly (string Id, string Name, string Description)[] RealmRoles =
    [
        ("20000000-0000-4000-8000-000000000001", IdentityRoles.Superuser, "超级用户"),
        ("20000000-0000-4000-8000-000000000002", IdentityRoles.Admin, "管理员"),
        ("20000000-0000-4000-8000-000000000003", IdentityRoles.Editor, "编辑用户"),
        ("20000000-0000-4000-8000-000000000004", IdentityRoles.Viewer, "普通用户")
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

    public Task StartAsync(CancellationToken cancellationToken)
        // 初始化失败时让 API 不就绪，阻止 WASM 在身份体系不完整时启动。
        => RunAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task RunAsync(CancellationToken ct = default)
    {
        var options = _options.CurrentValue;
        _logger.LogInformation("开始应用 Keycloak realm 引导配置：realm={Realm}", _client.Realm);

        // 默认组必须先存在，否则空 realm 更新 defaultGroups 会返回 500。
        await EnsureGroupsAsync(ct).ConfigureAwait(false);
        await EnsureRealmSettingsAsync(options, ct).ConfigureAwait(false);
        await EnsureUserProfileAsync(ct).ConfigureAwait(false);
        await EnsureRolesAsync(ct).ConfigureAwait(false);

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
        await EnsureSeedUsersAsync(options, ct).ConfigureAwait(false);

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
        node["loginWithEmailAllowed"] = false;
        node["registrationEmailAsUsername"] = false;
        node["resetPasswordAllowed"] = false;
        node["verifyEmail"] = false;
        node["bruteForceProtected"] = true;
        node["passwordPolicy"] =
            "length(8) and upperCase(1) and lowerCase(1) and digits(1) and specialChars(1)";
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
            throw new InvalidOperationException(
                $"更新 realm 设置失败：HTTP {response.StatusCode} {response.Body}");
    }

    private async Task EnsureUserProfileAsync(CancellationToken ct)
    {
        var current = await _client.GetAsync("users/profile", Module, ct).ConfigureAwait(false);
        ThrowIfUnauthorized(current, "读取 User Profile");
        if (!current.IsSuccess)
            throw new InvalidOperationException(
                $"读取 User Profile 失败：HTTP {current.StatusCode} {current.Body}");

        var config = JsonNode.Parse(current.Body)?.AsObject();
        if (config is null)
            throw new InvalidOperationException("User Profile 配置解析失败");

        config["unmanagedAttributePolicy"] = "ENABLED";
        if (config["attributes"] is JsonArray attributes)
        {
            foreach (var attribute in attributes.OfType<JsonObject>())
            {
                var name = (string?)attribute["name"];
                if (name is "email" or "firstName" or "lastName")
                    attribute.Remove("required");
            }
        }

        var response = await _client
            .SendAsync(HttpMethod.Put, "users/profile", config, Module, ct)
            .ConfigureAwait(false);
        ThrowIfUnauthorized(response, "启用未托管用户属性");
        if (!response.IsSuccess)
            throw new InvalidOperationException(
                $"启用未托管用户属性失败：HTTP {response.StatusCode} {response.Body}");
    }

    private async Task EnsureRolesAsync(CancellationToken ct)
    {
        foreach (var (id, name, description) in RealmRoles)
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
                    new { id, name, description, composite = false },
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

        // 固定 UUID 由空库时的声明式 realm 导入保证；Admin REST 创建组不接受 id。
        var response = await _client
            .SendAsync(HttpMethod.Post, "groups", new { name }, Module, ct)
            .ConfigureAwait(false);

        ThrowIfUnauthorized(response, $"创建组 {name}");
        if (!response.IsSuccess)
            throw new InvalidOperationException(
                $"创建组 {name} 失败：HTTP {response.StatusCode} {response.Body}");

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

        var existing = await FindClientUuidAsync(clientId, ct).ConfigureAwait(false);
        var id = existing ?? clientId switch
        {
            "treegraph-web" => "10000000-0000-4000-8000-000000000001",
            "treegraph-maui" => "10000000-0000-4000-8000-000000000002",
            "treegraph-wasm" => "10000000-0000-4000-8000-000000000003",
            "treegraph-admin" => "10000000-0000-4000-8000-000000000004",
            _ => null
        };
        var body = new
        {
            id,
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

    private async Task EnsureSeedUsersAsync(KeycloakAdminOptions options, CancellationToken ct)
    {
        var seeds = new List<SeedUser>
        {
            new(
                IdentityConstants.SuperuserId,
                IdentityConstants.SuperuserUsername,
                "超级用户",
                options.SeedSuperuserPassword,
                [IdentityRoles.Superuser, IdentityRoles.Admin, IdentityRoles.Editor],
                IsPhoneUser: false)
        };

        if (options.SeedDevelopmentUsers)
        {
            seeds.Add(new SeedUser(
                IdentityConstants.DevelopmentAdminId,
                IdentityConstants.DevelopmentAdminUsername,
                "管理员",
                options.SeedAdminPassword,
                [IdentityRoles.Admin, IdentityRoles.Editor],
                IsPhoneUser: false));
            seeds.Add(new SeedUser(
                IdentityConstants.DevelopmentUserId,
                IdentityConstants.DevelopmentUserUsername,
                "测试用户",
                options.SeedUserPassword,
                [IdentityRoles.Viewer],
                IsPhoneUser: true));
        }

        foreach (var seed in seeds)
            await EnsureSeedUserAsync(seed, ct).ConfigureAwait(false);
    }

    private async Task EnsureSeedUserAsync(SeedUser seed, CancellationToken ct)
    {
        var existing = await FindUserByUsernameAsync(seed.Username, ct).ConfigureAwait(false);
        string userId;
        if (existing is not null)
        {
            // 已存在账号不更新凭据、requiredActions、启用状态或资料，避免重启覆盖用户修改。
            userId = existing.Id
                ?? throw new InvalidOperationException($"预置账号 {seed.Username} 缺少用户 ID");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(seed.Password))
            {
                throw new InvalidOperationException(
                    $"缺少预置账号 {seed.Username} 的初始密码；请通过用户机密或环境变量注入");
            }

            var attributes = new Dictionary<string, string[]>
            {
                ["realName"] = [seed.RealName]
            };
            if (seed.IsPhoneUser)
                attributes["phoneNumber"] = [seed.Username];

            // 普通 POST /users 会忽略 id；partialImport 才能按固定 UUID 重建账号。
            var response = await _client.SendAsync(
                HttpMethod.Post,
                "partialImport",
                new
                {
                    ifResourceExists = "SKIP",
                    users = new[]
                    {
                        new
                        {
                            id = seed.Id,
                            username = seed.Username,
                            enabled = true,
                            attributes,
                            groups = new[] { IdentityConstants.DefaultGroupPath },
                            requiredActions = new[] { "UPDATE_PASSWORD" },
                            credentials = new[]
                            {
                                new { type = "password", value = seed.Password, temporary = true }
                            }
                        }
                    }
                },
                Module,
                ct).ConfigureAwait(false);

            if (!response.IsSuccess)
            {
                // 并发启动时另一实例可能已创建；只在确认账号确实存在后接受 409。
                var raced = response.StatusCode == 409
                    ? await FindUserByUsernameAsync(seed.Username, ct).ConfigureAwait(false)
                    : null;
                if (raced?.Id is null)
                    throw new InvalidOperationException(
                        $"创建预置账号 {seed.Username} 失败：HTTP {response.StatusCode} {response.Body}");
                userId = raced.Id;
            }
            else
            {
                var createdResponse = await _client
                    .GetAsync($"users/{seed.Id}", Module, ct)
                    .ConfigureAwait(false);
                var created = createdResponse.IsSuccess ? createdResponse.Deserialize<KcUser>() : null;
                if (created?.Id is null)
                    throw new InvalidOperationException(
                        $"创建预置账号 {seed.Username} 后未能按固定 ID 回查：HTTP {createdResponse.StatusCode} {response.Body}");
                if (!string.Equals(created.Username, seed.Username, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"预置账号 {seed.Username} 的固定 ID 已被其他账号占用：{created.Username}");
                if (!string.Equals(created.Username, seed.Username, StringComparison.Ordinal))
                {
                    // Keycloak 26.6 的 partialImport 会把用户名转为小写，新建后恢复约定的大小写。
                    var rename = await _client.SendAsync(
                        HttpMethod.Put,
                        $"users/{seed.Id}",
                        new { username = seed.Username },
                        Module,
                        ct).ConfigureAwait(false);
                    if (!rename.IsSuccess)
                        throw new InvalidOperationException(
                            $"校正预置账号 {seed.Username} 登录名失败：HTTP {rename.StatusCode} {rename.Body}");
                }
                userId = created.Id;
                _logger.LogInformation("已创建预置账号：{Username}", seed.Username);
            }
        }

        await EnsureUserRealmRolesAsync(userId, seed.Roles, ct).ConfigureAwait(false);
    }

    private async Task<KcUser?> FindUserByUsernameAsync(string username, CancellationToken ct)
    {
        var query = KeycloakAdminClient.BuildQuery(
            ("username", username),
            ("exact", "true"),
            ("briefRepresentation", "false"));
        var response = await _client.GetAsync("users" + query, Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            throw new InvalidOperationException($"查询预置账号 {username} 失败：HTTP {response.StatusCode}");

        return (response.Deserialize<List<KcUser>>() ?? [])
            .FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureUserRealmRolesAsync(
        string userId,
        IReadOnlyList<string> desiredRoleNames,
        CancellationToken ct)
    {
        var currentResponse = await _client
            .GetAsync($"users/{userId}/role-mappings/realm", Module, ct)
            .ConfigureAwait(false);
        if (!currentResponse.IsSuccess)
            throw new InvalidOperationException($"读取预置账号角色失败：HTTP {currentResponse.StatusCode}");

        var currentNames = (currentResponse.Deserialize<List<KcRole>>() ?? [])
            .Select(r => r.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet(StringComparer.Ordinal);
        var missing = new List<object>();
        foreach (var roleName in desiredRoleNames.Where(n => !currentNames.Contains(n)))
        {
            var roleResponse = await _client
                .GetAsync($"roles/{Uri.EscapeDataString(roleName)}", Module, ct)
                .ConfigureAwait(false);
            if (!roleResponse.IsSuccess)
                throw new InvalidOperationException($"读取预置角色 {roleName} 失败：HTTP {roleResponse.StatusCode}");

            var role = roleResponse.Deserialize<KcRole>();
            if (role?.Id is null)
                throw new InvalidOperationException($"预置角色 {roleName} 缺少角色 ID");
            missing.Add(new { id = role.Id, name = role.Name });
        }

        if (missing.Count == 0)
            return;

        var assign = await _client.SendAsync(
            HttpMethod.Post,
            $"users/{userId}/role-mappings/realm",
            missing,
            Module,
            ct).ConfigureAwait(false);
        if (!assign.IsSuccess)
            throw new InvalidOperationException($"分配预置账号角色失败：HTTP {assign.StatusCode}");
    }

    private sealed record SeedUser(
        string Id,
        string Username,
        string RealName,
        string Password,
        IReadOnlyList<string> Roles,
        bool IsPhoneUser);

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

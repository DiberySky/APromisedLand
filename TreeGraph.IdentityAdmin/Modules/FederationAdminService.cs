using System.Text.Json.Nodes;
using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>LDAP / AD 用户联邦管理实现。</summary>
public sealed class FederationAdminService : IFederationAdminService
{
    private const string Module = "user-federation";
    private const string ProviderType = "org.keycloak.storage.UserStorageProvider";
    private const string LdapProviderId = "ldap";

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;

    public FederationAdminService(KeycloakAdminClient client, AdminOperationRunner runner)
    {
        _client = client;
        _runner = runner;
    }

    public async Task<IReadOnlyList<UserFederationProviderSummary>> ListAsync(CancellationToken ct = default)
    {
        var query = KeycloakAdminClient.BuildQuery(("type", ProviderType));
        var response = await _client.GetAsync("components" + query, Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return [];

        var components = response.Deserialize<List<KcComponent>>() ?? [];
        return components.Select(c => new UserFederationProviderSummary(
            c.Id ?? string.Empty,
            c.Name ?? string.Empty,
            c.ProviderId ?? string.Empty,
            ReadBool(c, "enabled"))).ToArray();
    }

    public async Task<UserFederationProviderDetail?> GetAsync(string componentId, CancellationToken ct = default)
    {
        var component = await _client
            .GetJsonAsync<KcComponent>($"components/{componentId}", Module, ct)
            .ConfigureAwait(false);

        if (component is null)
            return null;

        return new UserFederationProviderDetail(
            component.Id ?? componentId,
            component.Name ?? string.Empty,
            component.ProviderId ?? string.Empty,
            ReadBool(component, "enabled"),
            ReadBool(component, "importEnabled"),
            string.Equals(ReadString(component, "editMode"), "WRITABLE", StringComparison.OrdinalIgnoreCase),
            ReadBool(component, "syncRegistrations"),
            ReadString(component, "vendor"),
            ReadString(component, "connectionUrl"),
            ReadString(component, "usersDn"),
            ReadString(component, "bindDn"),
            ReadString(component, "usernameLDAPAttribute"),
            ReadString(component, "rdnLDAPAttribute"),
            ReadString(component, "uuidLDAPAttribute"),
            ReadString(component, "userObjectClasses"),
            ReadString(component, "customUserSearchFilter"));
    }

    public async Task<AdminResult<string>> CreateAsync(
        AdminActor actor,
        SaveLdapFederationRequest request,
        CancellationToken ct = default)
    {
        var parentId = await GetRealmIdAsync(ct).ConfigureAwait(false);
        if (parentId is null)
            return AdminResult<string>.Fail("无法读取 realm 内部 ID", 502);

        var body = new
        {
            name = request.Name,
            providerId = LdapProviderId,
            providerType = ProviderType,
            parentId,
            config = BuildConfig(request, includeCredential: true)
        };

        return await _runner.ExecuteAsync<string>(
            actor,
            "federation.create",
            "user-federation",
            request.Name,
            request,
            async token =>
            {
                var response = await _client
                    .SendAsync(HttpMethod.Post, "components", body, Module, token)
                    .ConfigureAwait(false);
                return (response, response.ExtractCreatedId());
            },
            ct).ConfigureAwait(false);
    }

    public async Task<AdminResult> UpdateAsync(
        AdminActor actor,
        string componentId,
        SaveLdapFederationRequest request,
        CancellationToken ct = default)
    {
        var current = await _client.GetAsync($"components/{componentId}", Module, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return KeycloakStatusMapper.ToResult(current.StatusCode);

        var node = JsonNode.Parse(current.Body)?.AsObject();
        if (node is null)
            return AdminResult.Fail("联邦组件数据解析失败", 502);

        node["name"] = request.Name;

        var config = node["config"] as JsonObject ?? new JsonObject();
        foreach (var (key, values) in BuildConfig(request, includeCredential: !string.IsNullOrEmpty(request.BindCredential)))
        {
            var array = new JsonArray();
            foreach (var value in values)
                array.Add(value);

            config[key] = array;
        }

        node["config"] = config;

        return await _runner.ExecuteAsync(
            actor,
            "federation.update",
            "user-federation",
            componentId,
            request,
            token => _client.SendAsync(HttpMethod.Put, $"components/{componentId}", node, Module, token),
            ct).ConfigureAwait(false);
    }

    public Task<AdminResult> DeleteAsync(AdminActor actor, string componentId, CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "federation.delete",
            "user-federation",
            componentId,
            new { componentId },
            token => _client.SendAsync(HttpMethod.Delete, $"components/{componentId}", null, Module, token),
            ct);

    public async Task<FederationTestResult> TestConnectionAsync(
        SaveLdapFederationRequest request,
        CancellationToken ct = default)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("action", "testConnection"),
            new("connectionUrl", request.ConnectionUrl),
            new("authType", "simple"),
            new("useTruststoreSpi", "ldapsOnly"),
            new("connectionTimeout", "10000")
        };

        if (!string.IsNullOrEmpty(request.BindDn))
            fields.Add(new("bindDn", request.BindDn));

        if (!string.IsNullOrEmpty(request.BindCredential))
            fields.Add(new("bindCredential", request.BindCredential));

        var response = await _client
            .SendFormAsync(HttpMethod.Post, "testLDAPConnection", fields, Module, ct)
            .ConfigureAwait(false);

        return response.IsSuccess
            ? new FederationTestResult(true, "连接成功", response.StatusCode)
            : new FederationTestResult(false, KeycloakStatusMapper.ToMessage(response.StatusCode), response.StatusCode);
    }

    public async Task<AdminResult<FederationSyncResult>> SyncAsync(
        AdminActor actor,
        string componentId,
        CancellationToken ct = default)
        => await _runner.ExecuteAsync<FederationSyncResult>(
            actor,
            "federation.sync",
            "user-federation",
            componentId,
            new { componentId },
            async token =>
            {
                var response = await _client
                    .SendAsync(
                        HttpMethod.Post,
                        $"user-storage/{componentId}/sync?action=triggerFullSync",
                        null,
                        Module,
                        token)
                    .ConfigureAwait(false);

                var result = response.Deserialize<KcSynchronizationResult>();
                return (response, new FederationSyncResult(
                    result?.Added ?? 0,
                    result?.Updated ?? 0,
                    result?.Removed ?? 0,
                    result?.Failed ?? 0));
            },
            ct).ConfigureAwait(false);

    private async Task<string?> GetRealmIdAsync(CancellationToken ct)
    {
        var response = await _client.GetAsync(string.Empty, Module, ct).ConfigureAwait(false);
        return response.IsSuccess ? response.Deserialize<KcRealmRef>()?.Id : null;
    }

    private static Dictionary<string, string[]> BuildConfig(
        SaveLdapFederationRequest request,
        bool includeCredential)
    {
        var config = new Dictionary<string, string[]>
        {
            ["enabled"] = [Bool(request.Enabled)],
            ["importEnabled"] = [Bool(request.ImportEnabled)],
            ["editMode"] = [request.EditMode ? "WRITABLE" : "READ_ONLY"],
            ["syncRegistrations"] = [Bool(request.SyncRegistrations)],
            ["vendor"] = [request.Vendor ?? "other"],
            ["connectionUrl"] = [request.ConnectionUrl],
            ["usersDn"] = [request.UsersDn],
            ["usernameLDAPAttribute"] = [request.UsernameLdapAttribute ?? "uid"],
            ["rdnLDAPAttribute"] = [request.RdnLdapAttribute ?? "cn"],
            ["uuidLDAPAttribute"] = [request.UuidLdapAttribute ?? "entryUUID"],
            ["userObjectClasses"] = [request.UserObjectClasses ?? "inetOrgPerson, organizationalPerson"],
            ["authType"] = ["simple"],
            ["validatePasswordPolicy"] = ["false"],
            ["trustEmail"] = ["false"],
            ["cachePolicy"] = ["DEFAULT"],
            ["batchSizeForSync"] = ["1000"],
            ["fullSyncPeriod"] = ["-1"],
            ["changedSyncPeriod"] = ["-1"]
        };

        if (!string.IsNullOrEmpty(request.BindDn))
            config["bindDn"] = [request.BindDn];

        if (includeCredential && !string.IsNullOrEmpty(request.BindCredential))
            config["bindCredential"] = [request.BindCredential];

        if (!string.IsNullOrEmpty(request.CustomUserSearchFilter))
            config["customUserSearchFilter"] = [request.CustomUserSearchFilter];

        return config;
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static string? ReadString(KcComponent component, string key)
        => component.Config is not null && component.Config.TryGetValue(key, out var values) && values.Count > 0
            ? values[0]
            : null;

    private static bool ReadBool(KcComponent component, string key)
        => bool.TryParse(ReadString(component, key), out var value) && value;
}

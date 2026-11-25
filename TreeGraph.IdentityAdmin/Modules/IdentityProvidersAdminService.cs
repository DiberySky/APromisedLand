using System.Text.Json.Nodes;
using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>身份提供方（OIDC/SAML/社交）管理实现。</summary>
public sealed class IdentityProvidersAdminService : IIdentityProvidersAdminService
{
    private const string Module = "identity-providers";
    private const string BasePath = "identity-provider/instances";

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;

    public IdentityProvidersAdminService(KeycloakAdminClient client, AdminOperationRunner runner)
    {
        _client = client;
        _runner = runner;
    }

    public async Task<IReadOnlyList<IdentityProviderSummary>> ListAsync(CancellationToken ct = default)
    {
        var response = await _client.GetAsync(BasePath, Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return [];

        var providers = response.Deserialize<List<KcIdentityProvider>>() ?? [];
        return providers.Select(p => new IdentityProviderSummary(
            p.Alias ?? string.Empty,
            p.ProviderId ?? string.Empty,
            p.DisplayName,
            p.Enabled ?? false)).ToArray();
    }

    public async Task<IdentityProviderDetail?> GetAsync(string alias, CancellationToken ct = default)
    {
        var provider = await _client
            .GetJsonAsync<KcIdentityProvider>($"{BasePath}/{alias}", Module, ct)
            .ConfigureAwait(false);

        return provider is null
            ? null
            : new IdentityProviderDetail(
                provider.Alias ?? alias,
                provider.ProviderId ?? string.Empty,
                provider.DisplayName,
                provider.Enabled ?? false,
                provider.TrustEmail ?? false,
                provider.StoreToken ?? false,
                provider.LinkOnly ?? false,
                (IReadOnlyDictionary<string, string>?)provider.Config ?? new Dictionary<string, string>());
    }

    public Task<AdminResult> CreateAsync(
        AdminActor actor,
        SaveIdentityProviderRequest request,
        CancellationToken ct = default)
    {
        var body = new
        {
            alias = request.Alias,
            providerId = request.ProviderId,
            displayName = request.DisplayName,
            enabled = request.Enabled,
            trustEmail = request.TrustEmail,
            storeToken = request.StoreToken,
            linkOnly = request.LinkOnly,
            config = request.Config ?? new Dictionary<string, string>()
        };

        return _runner.ExecuteAsync(
            actor,
            "idp.create",
            "identity-provider",
            request.Alias,
            request,
            token => _client.SendAsync(HttpMethod.Post, BasePath, body, Module, token),
            ct);
    }

    public async Task<AdminResult> UpdateAsync(
        AdminActor actor,
        string alias,
        SaveIdentityProviderRequest request,
        CancellationToken ct = default)
    {
        var current = await _client.GetAsync($"{BasePath}/{alias}", Module, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return KeycloakStatusMapper.ToResult(current.StatusCode);

        var node = JsonNode.Parse(current.Body)?.AsObject();
        if (node is null)
            return AdminResult.Fail("身份提供方数据解析失败", 502);

        node["displayName"] = request.DisplayName;
        node["enabled"] = request.Enabled;
        node["trustEmail"] = request.TrustEmail;
        node["storeToken"] = request.StoreToken;
        node["linkOnly"] = request.LinkOnly;

        if (request.Config is not null)
        {
            var config = node["config"] as JsonObject ?? new JsonObject();
            foreach (var (key, value) in request.Config)
            {
                if (value.Length > 0)
                    config[key] = value;
            }

            node["config"] = config;
        }

        return await _runner.ExecuteAsync(
            actor,
            "idp.update",
            "identity-provider",
            alias,
            request,
            token => _client.SendAsync(HttpMethod.Put, $"{BasePath}/{alias}", node, Module, token),
            ct).ConfigureAwait(false);
    }

    public Task<AdminResult> DeleteAsync(AdminActor actor, string alias, CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "idp.delete",
            "identity-provider",
            alias,
            new { alias },
            token => _client.SendAsync(HttpMethod.Delete, $"{BasePath}/{alias}", null, Module, token),
            ct);

    public async Task<IReadOnlyList<IdentityProviderMapperInfo>> ListMappersAsync(
        string alias,
        CancellationToken ct = default)
    {
        var response = await _client.GetAsync($"{BasePath}/{alias}/mappers", Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return [];

        var mappers = response.Deserialize<List<KcIdentityProviderMapper>>() ?? [];
        return mappers.Select(m => new IdentityProviderMapperInfo(
            m.Id ?? string.Empty,
            m.Name ?? string.Empty,
            m.IdentityProviderAlias ?? alias,
            m.IdentityProviderMapper ?? string.Empty,
            (IReadOnlyDictionary<string, string>?)m.Config ?? new Dictionary<string, string>())).ToArray();
    }

    public Task<AdminResult<string>> CreateMapperAsync(
        AdminActor actor,
        string alias,
        IdentityProviderMapperInfo mapper,
        CancellationToken ct = default)
    {
        var body = new
        {
            name = mapper.Name,
            identityProviderAlias = alias,
            identityProviderMapper = mapper.IdentityProviderMapper,
            config = mapper.Config
        };

        return _runner.ExecuteAsync<string>(
            actor,
            "idp.create-mapper",
            "identity-provider",
            alias,
            mapper,
            async token =>
            {
                var response = await _client
                    .SendAsync(HttpMethod.Post, $"{BasePath}/{alias}/mappers", body, Module, token)
                    .ConfigureAwait(false);
                return (response, response.ExtractCreatedId());
            },
            ct);
    }

    public Task<AdminResult> DeleteMapperAsync(
        AdminActor actor,
        string alias,
        string mapperId,
        CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "idp.delete-mapper",
            "identity-provider",
            alias,
            new { alias, mapperId },
            token => _client.SendAsync(
                HttpMethod.Delete,
                $"{BasePath}/{alias}/mappers/{mapperId}",
                null,
                Module,
                token),
            ct);
}

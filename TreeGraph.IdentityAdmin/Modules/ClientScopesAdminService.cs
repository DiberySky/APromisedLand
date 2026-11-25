using System.Text.Json.Nodes;
using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>client scope 与 protocol mapper 管理实现。</summary>
public sealed class ClientScopesAdminService : IClientScopesAdminService
{
    private const string Module = "client-scopes";

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;

    public ClientScopesAdminService(KeycloakAdminClient client, AdminOperationRunner runner)
    {
        _client = client;
        _runner = runner;
    }

    public async Task<IReadOnlyList<IdentityClientScope>> ListAsync(CancellationToken ct = default)
    {
        var response = await _client.GetAsync("client-scopes", Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return [];

        var scopes = response.Deserialize<List<KcClientScope>>() ?? [];
        var defaultNames = await GetDefaultScopeNamesAsync(ct).ConfigureAwait(false);

        return scopes
            .Select(s => new IdentityClientScope(
                s.Id ?? string.Empty,
                s.Name ?? string.Empty,
                s.Description,
                s.Protocol,
                s.Name is not null && defaultNames.Contains(s.Name)))
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IdentityClientScope?> GetAsync(string scopeId, CancellationToken ct = default)
    {
        var scope = await _client.GetJsonAsync<KcClientScope>($"client-scopes/{scopeId}", Module, ct)
            .ConfigureAwait(false);

        return scope is null
            ? null
            : new IdentityClientScope(
                scope.Id ?? scopeId,
                scope.Name ?? string.Empty,
                scope.Description,
                scope.Protocol,
                false);
    }

    public Task<AdminResult<string>> CreateAsync(
        AdminActor actor,
        SaveClientScopeRequest request,
        CancellationToken ct = default)
    {
        var body = new
        {
            name = request.Name,
            description = request.Description,
            protocol = request.Protocol ?? "openid-connect"
        };

        return _runner.ExecuteAsync<string>(
            actor,
            "client-scope.create",
            "client-scope",
            request.Name,
            request,
            async token =>
            {
                var response = await _client
                    .SendAsync(HttpMethod.Post, "client-scopes", body, Module, token)
                    .ConfigureAwait(false);
                return (response, response.ExtractCreatedId());
            },
            ct);
    }

    public async Task<AdminResult> UpdateAsync(
        AdminActor actor,
        string scopeId,
        SaveClientScopeRequest request,
        CancellationToken ct = default)
    {
        var current = await _client.GetAsync($"client-scopes/{scopeId}", Module, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return KeycloakStatusMapper.ToResult(current.StatusCode);

        var node = JsonNode.Parse(current.Body)?.AsObject();
        if (node is null)
            return AdminResult.Fail("client scope 数据解析失败", 502);

        node["name"] = request.Name;
        if (request.Description is not null)
            node["description"] = request.Description;

        return await _runner.ExecuteAsync(
            actor,
            "client-scope.update",
            "client-scope",
            scopeId,
            request,
            token => _client.SendAsync(HttpMethod.Put, $"client-scopes/{scopeId}", node, Module, token),
            ct).ConfigureAwait(false);
    }

    public Task<AdminResult> DeleteAsync(AdminActor actor, string scopeId, CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "client-scope.delete",
            "client-scope",
            scopeId,
            new { scopeId },
            token => _client.SendAsync(HttpMethod.Delete, $"client-scopes/{scopeId}", null, Module, token),
            ct);

    public async Task<IReadOnlyList<IdentityProtocolMapper>> ListMappersAsync(
        string scopeId,
        CancellationToken ct = default)
    {
        var response = await _client
            .GetAsync($"client-scopes/{scopeId}/protocol-mappers/models", Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            return [];

        var mappers = response.Deserialize<List<KcProtocolMapper>>() ?? [];
        return mappers.Select(m => new IdentityProtocolMapper(
            m.Id ?? string.Empty,
            m.Name ?? string.Empty,
            m.Protocol ?? "openid-connect",
            m.ProtocolMapper ?? string.Empty,
            (IReadOnlyDictionary<string, string>?)m.Config
                ?? new Dictionary<string, string>())).ToArray();
    }

    public Task<AdminResult<string>> CreateMapperAsync(
        AdminActor actor,
        string scopeId,
        SaveProtocolMapperRequest request,
        CancellationToken ct = default)
    {
        var body = new
        {
            name = request.Name,
            protocol = request.Protocol,
            protocolMapper = request.ProtocolMapper,
            config = request.Config ?? new Dictionary<string, string>()
        };

        return _runner.ExecuteAsync<string>(
            actor,
            "client-scope.create-mapper",
            "client-scope",
            scopeId,
            request,
            async token =>
            {
                var response = await _client
                    .SendAsync(
                        HttpMethod.Post,
                        $"client-scopes/{scopeId}/protocol-mappers/models",
                        body,
                        Module,
                        token)
                    .ConfigureAwait(false);
                return (response, response.ExtractCreatedId());
            },
            ct);
    }

    public Task<AdminResult> UpdateMapperAsync(
        AdminActor actor,
        string scopeId,
        string mapperId,
        SaveProtocolMapperRequest request,
        CancellationToken ct = default)
    {
        var body = new
        {
            id = mapperId,
            name = request.Name,
            protocol = request.Protocol,
            protocolMapper = request.ProtocolMapper,
            config = request.Config ?? new Dictionary<string, string>()
        };

        return _runner.ExecuteAsync(
            actor,
            "client-scope.update-mapper",
            "client-scope",
            scopeId,
            request,
            token => _client.SendAsync(
                HttpMethod.Put,
                $"client-scopes/{scopeId}/protocol-mappers/models/{mapperId}",
                body,
                Module,
                token),
            ct);
    }

    public Task<AdminResult> DeleteMapperAsync(
        AdminActor actor,
        string scopeId,
        string mapperId,
        CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "client-scope.delete-mapper",
            "client-scope",
            scopeId,
            new { scopeId, mapperId },
            token => _client.SendAsync(
                HttpMethod.Delete,
                $"client-scopes/{scopeId}/protocol-mappers/models/{mapperId}",
                null,
                Module,
                token),
            ct);

    public Task<AdminResult> AddDefaultScopeToClientAsync(
        AdminActor actor,
        string clientId,
        string scopeId,
        CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "client.add-default-scope",
            "client",
            clientId,
            new { clientId, scopeId },
            token => _client.SendAsync(
                HttpMethod.Put,
                $"clients/{clientId}/default-client-scopes/{scopeId}",
                null,
                Module,
                token),
            ct);

    public Task<AdminResult> AddOptionalScopeToClientAsync(
        AdminActor actor,
        string clientId,
        string scopeId,
        CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "client.add-optional-scope",
            "client",
            clientId,
            new { clientId, scopeId },
            token => _client.SendAsync(
                HttpMethod.Put,
                $"clients/{clientId}/optional-client-scopes/{scopeId}",
                null,
                Module,
                token),
            ct);

    public Task<AdminResult> RemoveDefaultScopeFromClientAsync(
        AdminActor actor,
        string clientId,
        string scopeId,
        CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "client.remove-default-scope",
            "client",
            clientId,
            new { clientId, scopeId },
            token => _client.SendAsync(
                HttpMethod.Delete,
                $"clients/{clientId}/default-client-scopes/{scopeId}",
                null,
                Module,
                token),
            ct);

    public Task<AdminResult> RemoveOptionalScopeFromClientAsync(
        AdminActor actor,
        string clientId,
        string scopeId,
        CancellationToken ct = default)
        => _runner.ExecuteAsync(
            actor,
            "client.remove-optional-scope",
            "client",
            clientId,
            new { clientId, scopeId },
            token => _client.SendAsync(
                HttpMethod.Delete,
                $"clients/{clientId}/optional-client-scopes/{scopeId}",
                null,
                Module,
                token),
            ct);

    private async Task<HashSet<string>> GetDefaultScopeNamesAsync(CancellationToken ct)
    {
        var response = await _client
            .GetAsync("default-default-client-scopes", Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            return new HashSet<string>(StringComparer.Ordinal);

        var scopes = response.Deserialize<List<KcClientScope>>() ?? [];
        return scopes
            .Select(s => s.Name ?? string.Empty)
            .Where(n => n.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }
}

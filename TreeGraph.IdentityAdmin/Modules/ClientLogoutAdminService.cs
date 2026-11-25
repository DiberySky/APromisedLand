using System.Text.Json.Nodes;
using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>客户端登出传播（back-channel / front-channel）配置实现。</summary>
public sealed class ClientLogoutAdminService : IClientLogoutAdminService
{
    private const string Module = "client-logout";

    private const string BackchannelSessionKey = "backchannel.logout.session.required";
    private const string BackchannelRevokeKey = "backchannel.logout.revoke.offline.tokens";
    private const string FrontchannelSessionKey = "frontchannel.logout.session.required";
    private const string FrontchannelUrlKey = "frontchannel.logout.url";

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;

    public ClientLogoutAdminService(KeycloakAdminClient client, AdminOperationRunner runner)
    {
        _client = client;
        _runner = runner;
    }

    public async Task<ClientLogoutSettings?> GetAsync(string clientId, CancellationToken ct = default)
    {
        var client = await FindClientAsync(clientId, ct).ConfigureAwait(false);
        if (client is null)
            return null;

        var attributes = client.Attributes ?? new Dictionary<string, string>();

        return new ClientLogoutSettings(
            client.ClientId ?? clientId,
            ReadBool(attributes, BackchannelSessionKey),
            ReadBool(attributes, BackchannelRevokeKey),
            ReadBool(attributes, FrontchannelSessionKey),
            attributes.TryGetValue(FrontchannelUrlKey, out var url) ? url : null);
    }

    public async Task<AdminResult> UpdateAsync(
        AdminActor actor,
        string clientId,
        UpdateClientLogoutSettingsRequest request,
        CancellationToken ct = default)
    {
        var existing = await FindClientAsync(clientId, ct).ConfigureAwait(false);
        if (existing?.Id is null)
            return AdminResult.Fail("客户端不存在（404）", 404);

        var current = await _client.GetAsync($"clients/{existing.Id}", Module, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return KeycloakStatusMapper.ToResult(current.StatusCode);

        var node = JsonNode.Parse(current.Body)?.AsObject();
        if (node is null)
            return AdminResult.Fail("客户端数据解析失败", 502);

        var attributes = node["attributes"] as JsonObject ?? new JsonObject();

        if (request.BackchannelLogoutSessionRequired is { } backchannelSession)
            attributes[BackchannelSessionKey] = Bool(backchannelSession);

        if (request.BackchannelLogoutRevokeOfflineTokens is { } revokeOffline)
            attributes[BackchannelRevokeKey] = Bool(revokeOffline);

        if (request.FrontchannelLogoutSessionRequired is { } frontchannelSession)
            attributes[FrontchannelSessionKey] = Bool(frontchannelSession);

        if (request.FrontchannelLogoutUrl is not null)
            attributes[FrontchannelUrlKey] = request.FrontchannelLogoutUrl;

        node["attributes"] = attributes;

        // Keycloak 的 client 更新端点要求内部 uuid；clientId 仅用于查询。
        var clientUuid = (string?)node["id"] is { Length: > 0 } id ? id : clientId;

        return await _runner.ExecuteAsync(
            actor,
            "client.update-logout",
            "client",
            clientId,
            request,
            token => _client.SendAsync(HttpMethod.Put, $"clients/{clientUuid}", node, Module, token),
            ct).ConfigureAwait(false);
    }

    private async Task<KcClient?> FindClientAsync(string clientId, CancellationToken ct)
    {
        // 优先按 uuid 直接读取；若传入的是 clientId 字符串则回退到列表查询。
        var direct = await _client.GetJsonAsync<KcClient>($"clients/{clientId}", Module, ct).ConfigureAwait(false);
        if (direct is not null)
            return direct;

        var query = KeycloakAdminClient.BuildQuery(("clientId", clientId), ("exact", "true"));
        var list = await _client.GetAsync("clients" + query, Module, ct).ConfigureAwait(false);
        return list.IsSuccess
            ? (list.Deserialize<List<KcClient>>() ?? []).FirstOrDefault()
            : null;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> attributes, string key)
        => attributes.TryGetValue(key, out var value)
           && bool.TryParse(value, out var parsed)
           && parsed;

    private static string Bool(bool value) => value ? "true" : "false";
}

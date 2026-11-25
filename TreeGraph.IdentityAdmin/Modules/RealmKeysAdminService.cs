using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>realm 签名/加密密钥管理实现。</summary>
public sealed class RealmKeysAdminService : IRealmKeysAdminService
{
    private const string Module = "realm-keys";
    private const string KeyProviderType = "org.keycloak.keys.KeyProvider";

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;

    public RealmKeysAdminService(KeycloakAdminClient client, AdminOperationRunner runner)
    {
        _client = client;
        _runner = runner;
    }

    public async Task<IReadOnlyList<RealmSigningKey>> ListAsync(CancellationToken ct = default)
    {
        var keys = await LoadKeysAsync(ct).ConfigureAwait(false);
        return keys.Select(Map).ToArray();
    }

    public async Task<AdminResult<RealmSigningKey>> GenerateAsync(
        AdminActor actor,
        GenerateRealmKeyRequest request,
        CancellationToken ct = default)
    {
        var realmId = await GetRealmIdAsync(ct).ConfigureAwait(false);
        if (realmId is null)
            return AdminResult<RealmSigningKey>.Fail("无法读取 realm 内部 ID", 502);

        var body = new
        {
            name = string.Concat("generated-", request.ProviderId, "-", DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss")),
            providerId = request.ProviderId,
            providerType = KeyProviderType,
            parentId = realmId,
            config = new Dictionary<string, string[]>
            {
                ["priority"] = [request.Priority.ToString()],
                ["enabled"] = ["true"],
                ["active"] = ["true"],
                ["algorithm"] = [request.Algorithm],
                ["keySize"] = [request.KeySize.ToString()]
            }
        };

        return await _runner.ExecuteAsync<RealmSigningKey>(
            actor,
            "realm-key.generate",
            "realm-key",
            request.ProviderId,
            request,
            async token =>
            {
                var created = await _client
                    .SendAsync(HttpMethod.Post, "components", body, Module, token)
                    .ConfigureAwait(false);

                if (!created.IsSuccess)
                    return (created, (RealmSigningKey?)null);

                var componentId = created.ExtractCreatedId();
                if (string.IsNullOrEmpty(componentId))
                    return (created, (RealmSigningKey?)null);

                var keys = await LoadKeysAsync(token).ConfigureAwait(false);
                var match = keys.FirstOrDefault(k =>
                    string.Equals(k.ProviderId, componentId, StringComparison.Ordinal));

                return (created, match is null ? null : Map(match));
            },
            ct).ConfigureAwait(false);
    }

    public async Task<AdminResult> DeleteAsync(
        AdminActor actor,
        string providerId,
        CancellationToken ct = default)
    {
        var keys = await LoadKeysAsync(ct).ConfigureAwait(false);
        var target = keys.FirstOrDefault(k => string.Equals(k.ProviderId, providerId, StringComparison.Ordinal));

        if (target is null)
            return AdminResult.Fail("密钥不存在（404）", 404);

        if (string.Equals(target.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            return AdminResult.Fail("不能删除活动签名密钥，请先生成新密钥使其转为 PASSIVE", 409);

        return await _runner.ExecuteAsync(
            actor,
            "realm-key.delete",
            "realm-key",
            providerId,
            new { providerId },
            token => _client.SendAsync(HttpMethod.Delete, $"components/{providerId}", null, Module, token),
            ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<KcKeyMetadata>> LoadKeysAsync(CancellationToken ct)
    {
        var response = await _client.GetAsync("keys", Module, ct).ConfigureAwait(false);
        return response.IsSuccess
            ? response.Deserialize<List<KcKeyMetadata>>() ?? []
            : [];
    }

    private async Task<string?> GetRealmIdAsync(CancellationToken ct)
    {
        var response = await _client.GetAsync(string.Empty, Module, ct).ConfigureAwait(false);
        return response.IsSuccess ? response.Deserialize<KcRealmRef>()?.Id : null;
    }

    private static RealmSigningKey Map(KcKeyMetadata key)
        => new(
            key.ProviderId ?? string.Empty,
            key.Kid,
            key.Type,
            key.Algorithm,
            key.Status,
            key.ProviderPriority,
            key.PublicKey,
            key.Certificate,
            key.ValidTo?.ToString());
}

using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Options;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>管理台仪表盘聚合实现。</summary>
public sealed class DashboardAdminService : IDashboardAdminService
{
    private const string Module = "dashboard";

    private readonly KeycloakAdminClient _client;
    private readonly IAdminAuditQueryService _auditQuery;
    private readonly IOptionsMonitor<KeycloakAdminOptions> _options;

    public DashboardAdminService(
        KeycloakAdminClient client,
        IAdminAuditQueryService auditQuery,
        IOptionsMonitor<KeycloakAdminOptions> options)
    {
        _client = client;
        _auditQuery = auditQuery;
        _options = options;
    }

    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var userCount = await GetCountAsync("users/count", ct).ConfigureAwait(false);
        var enabledUserCount = await GetCountAsync("users/count?enabled=true", ct).ConfigureAwait(false);
        var groupCount = await GetCountAsync("groups/count", ct).ConfigureAwait(false);
        var roleCount = await GetListCountAsync("roles?briefRepresentation=true", ct).ConfigureAwait(false);
        var clientCount = await GetListCountAsync("clients?first=0&max=1000", ct).ConfigureAwait(false);
        var activeSessions = await GetActiveSessionCountAsync(ct).ConfigureAwait(false);

        var (version, matches) = await GetVersionAsync(ct).ConfigureAwait(false);
        var recent = await _auditQuery
            .QueryAsync(new AdminAuditQuery(PageSize: 5), ct)
            .ConfigureAwait(false);

        return new DashboardSummary(
            userCount,
            enabledUserCount > 0 ? enabledUserCount : userCount,
            activeSessions,
            clientCount,
            groupCount,
            roleCount,
            version,
            version is not null && matches,
            recent.Items);
    }

    public async Task<(string? Version, bool MatchesExpected)> GetVersionAsync(CancellationToken ct = default)
    {
        var response = await _client
            .SendAdminRootAsync(HttpMethod.Get, "serverinfo", Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            return (null, false);

        var node = JsonNode.Parse(response.Body)?.AsObject();
        var version = node?["systemInfo"]?["version"]?.GetValue<string>();
        var expected = _options.CurrentValue.ExpectedVersion;
        return (version, string.Equals(version, expected, StringComparison.Ordinal));
    }

    private async Task<int> GetCountAsync(string path, CancellationToken ct)
    {
        var response = await _client.GetAsync(path, Module, ct).ConfigureAwait(false);
        return response.IsSuccess && int.TryParse(response.Body, out var value) ? value : 0;
    }

    private async Task<int> GetListCountAsync(string path, CancellationToken ct)
    {
        var response = await _client.GetAsync(path, Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return 0;

        var node = JsonNode.Parse(response.Body);
        return node is JsonArray array ? array.Count : 0;
    }

    private async Task<int> GetActiveSessionCountAsync(CancellationToken ct)
    {
        var response = await _client.GetAsync("client-session-stats", Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return 0;

        var node = JsonNode.Parse(response.Body);
        if (node is not JsonArray array)
            return 0;

        var total = 0;
        foreach (var item in array)
        {
            if (item?["active"] is JsonValue value && value.TryGetValue<int>(out var active))
                total += active;
        }

        return total;
    }
}

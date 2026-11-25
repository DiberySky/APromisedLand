using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>realm partial export 实现。</summary>
public sealed class ExportAdminService : IExportAdminService
{
    private const string Module = "export";

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;
    private readonly TimeProvider _timeProvider;

    public ExportAdminService(
        KeycloakAdminClient client,
        AdminOperationRunner runner,
        TimeProvider timeProvider)
    {
        _client = client;
        _runner = runner;
        _timeProvider = timeProvider;
    }

    public Task<AdminResult<RealmExportResult>> ExportAsync(
        AdminActor actor,
        RealmExportOptions options,
        CancellationToken ct = default)
    {
        var query = KeycloakAdminClient.BuildQuery(
            ("exportGroupsAndRoles", options.ExportGroupsAndRoles ? "true" : "false"),
            ("exportClients", options.ExportClients ? "true" : "false"),
            ("exportClientsIncludingSecrets", options.ExportClientsIncludingSecrets ? "true" : "false"));

        return _runner.ExecuteAsync<RealmExportResult>(
            actor,
            "realm.export",
            "realm",
            _client.Realm,
            options,
            async token =>
            {
                var response = await _client
                    .SendAsync(HttpMethod.Post, "partial-export" + query, null, Module, token)
                    .ConfigureAwait(false);

                var exportedAt = _timeProvider.GetUtcNow();
                var fileName = string.Concat(
                    _client.Realm,
                    "-partial-",
                    exportedAt.ToString("yyyyMMddHHmmss"),
                    ".json");

                return (response, new RealmExportResult(response.Body, fileName, exportedAt));
            },
            ct);
    }
}

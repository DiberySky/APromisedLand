using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules.Internal;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>管理与登录事件查询实现。</summary>
public sealed class EventsAdminService : IEventsAdminService
{
    private const string Module = "events";

    private readonly KeycloakAdminClient _client;

    public EventsAdminService(KeycloakAdminClient client) => _client = client;

    public async Task<IReadOnlyList<AdminEventEntry>> QueryAdminEventsAsync(
        AdminEventQuery query,
        CancellationToken ct = default)
    {
        var path = "admin-events" + KeycloakAdminClient.BuildQuery(
            ("operationTypes", query.OperationType),
            ("resourceTypes", query.ResourceType),
            ("authUser", query.UserId),
            ("client", query.ClientId),
            ("dateFrom", query.DateFrom),
            ("dateTo", query.DateTo),
            ("first", query.First.ToString()),
            ("max", query.Max.ToString()));

        var response = await _client.GetAsync(path, Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return [];

        var events = response.Deserialize<List<KcAdminEvent>>() ?? [];
        return events.Select(e => new AdminEventEntry(
            ToIso(e.Time),
            e.OperationType,
            e.ResourcePath,
            e.ResourceType,
            e.Error,
            e.IpAddress,
            e.AuthDetails?.ClientId,
            e.AuthDetails?.UserId)).ToArray();
    }

    public async Task<IReadOnlyList<LoginEventEntry>> QueryLoginEventsAsync(
        LoginEventQuery query,
        CancellationToken ct = default)
    {
        var path = "events" + KeycloakAdminClient.BuildQuery(
            ("type", query.Type),
            ("user", query.UserId),
            ("client", query.ClientId),
            ("dateFrom", query.DateFrom),
            ("dateTo", query.DateTo),
            ("first", query.First.ToString()),
            ("max", query.Max.ToString()));

        var response = await _client.GetAsync(path, Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return [];

        var events = response.Deserialize<List<KcEvent>>() ?? [];
        return events.Select(e => new LoginEventEntry(
            ToIso(e.Time),
            e.Type,
            e.ClientId,
            e.UserId,
            e.SessionId,
            e.IpAddress,
            e.Error,
            (IReadOnlyDictionary<string, string>?)e.Details ?? new Dictionary<string, string>())).ToArray();
    }

    private static string? ToIso(long? milliseconds)
        => milliseconds is null
            ? null
            : DateTimeOffset.FromUnixTimeMilliseconds(milliseconds.Value).ToString("O");
}

using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.Blazor.Infrastructure.Admin;

public sealed partial class IdentityAdminApiClient
{
    Task<IReadOnlyList<IdentityProviderSummary>> IIdentityProvidersAdminService.ListAsync(CancellationToken ct)
        => GetRequiredAsync<IReadOnlyList<IdentityProviderSummary>>("identity-providers", ct);

    Task<IdentityProviderDetail?> IIdentityProvidersAdminService.GetAsync(string alias, CancellationToken ct)
        => GetOrDefaultAsync<IdentityProviderDetail>("identity-providers/" + Id(alias), ct);

    Task<AdminResult> IIdentityProvidersAdminService.CreateAsync(AdminActor actor, SaveIdentityProviderRequest request, CancellationToken ct)
        => SendResultAsync(HttpMethod.Post, "identity-providers", request, ct);

    Task<AdminResult> IIdentityProvidersAdminService.UpdateAsync(AdminActor actor, string alias, SaveIdentityProviderRequest request, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "identity-providers/" + Id(alias), request, ct);

    Task<AdminResult> IIdentityProvidersAdminService.DeleteAsync(AdminActor actor, string alias, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "identity-providers/" + Id(alias), null, ct);

    Task<IReadOnlyList<IdentityProviderMapperInfo>> IIdentityProvidersAdminService.ListMappersAsync(string alias, CancellationToken ct)
        => GetRequiredAsync<IReadOnlyList<IdentityProviderMapperInfo>>("identity-providers/" + Id(alias) + "/mappers", ct);

    Task<AdminResult<string>> IIdentityProvidersAdminService.CreateMapperAsync(AdminActor actor, string alias, IdentityProviderMapperInfo mapper, CancellationToken ct)
        => SendResultAsync<string>(HttpMethod.Post, "identity-providers/" + Id(alias) + "/mappers", mapper, ct);

    Task<AdminResult> IIdentityProvidersAdminService.DeleteMapperAsync(AdminActor actor, string alias, string mapperId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "identity-providers/" + Id(alias) + "/mappers/" + Id(mapperId), null, ct);

    Task<IReadOnlyList<UserFederationProviderSummary>> IFederationAdminService.ListAsync(CancellationToken ct)
        => GetRequiredAsync<IReadOnlyList<UserFederationProviderSummary>>("user-federation", ct);

    Task<UserFederationProviderDetail?> IFederationAdminService.GetAsync(string componentId, CancellationToken ct)
        => GetOrDefaultAsync<UserFederationProviderDetail>("user-federation/" + Id(componentId), ct);

    Task<AdminResult<string>> IFederationAdminService.CreateAsync(AdminActor actor, SaveLdapFederationRequest request, CancellationToken ct)
        => SendResultAsync<string>(HttpMethod.Post, "user-federation", request, ct);

    Task<AdminResult> IFederationAdminService.UpdateAsync(AdminActor actor, string componentId, SaveLdapFederationRequest request, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "user-federation/" + Id(componentId), request, ct);

    Task<AdminResult> IFederationAdminService.DeleteAsync(AdminActor actor, string componentId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "user-federation/" + Id(componentId), null, ct);

    Task<FederationTestResult> IFederationAdminService.TestConnectionAsync(SaveLdapFederationRequest request, CancellationToken ct)
        => PostReadAsync<FederationTestResult>("user-federation/test-connection", request, ct);

    Task<AdminResult<FederationSyncResult>> IFederationAdminService.SyncAsync(AdminActor actor, string componentId, CancellationToken ct)
        => SendResultAsync<FederationSyncResult>(HttpMethod.Post, "user-federation/" + Id(componentId) + "/sync", null, ct);

    Task<IReadOnlyList<AuthenticationFlowSummary>> IAuthFlowsAdminService.ListAsync(CancellationToken ct)
        => GetRequiredAsync<IReadOnlyList<AuthenticationFlowSummary>>("auth-flows", ct);

    Task<AuthenticationFlowDetail?> IAuthFlowsAdminService.GetAsync(string alias, CancellationToken ct)
        => GetOrDefaultAsync<AuthenticationFlowDetail>("auth-flows/" + Id(alias), ct);

    Task<AdminResult<string>> IAuthFlowsAdminService.CopyAsync(AdminActor actor, CopyAuthenticationFlowRequest request, CancellationToken ct)
        => SendResultAsync<string>(HttpMethod.Post, "auth-flows/copy", request, ct);

    Task<AdminResult> IAuthFlowsAdminService.DeleteAsync(AdminActor actor, string alias, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "auth-flows/" + Id(alias), null, ct);

    Task<AdminResult> IAuthFlowsAdminService.UpdateExecutionAsync(AdminActor actor, string flowAlias, string executionId, string requirement, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "auth-flows/" + Id(flowAlias) + "/executions/" + Id(executionId), new { requirement }, ct);

    Task<IReadOnlyList<AuthenticationFlowBinding>> IAuthFlowsAdminService.ListBindingsAsync(CancellationToken ct)
        => GetRequiredAsync<IReadOnlyList<AuthenticationFlowBinding>>("auth-flow-bindings", ct);

    Task<AdminResult> IAuthFlowsAdminService.UpdateBindingAsync(AdminActor actor, string bindingType, string flowAlias, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "auth-flow-bindings/" + Id(bindingType), new { flowAlias }, ct);

    Task<RealmSettings?> IRealmSettingsAdminService.GetAsync(CancellationToken ct)
        => GetOrDefaultAsync<RealmSettings>("realm", ct);

    Task<AdminResult> IRealmSettingsAdminService.UpdateAsync(AdminActor actor, UpdateRealmSettingsRequest request, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "realm", request, ct);

    Task<AdminResult> IRealmSettingsAdminService.UpdateSmtpAsync(AdminActor actor, SmtpSettingsRequest request, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "realm/smtp", request, ct);

    Task<AvailableThemes> IRealmSettingsAdminService.GetAvailableThemesAsync(CancellationToken ct)
        => GetRequiredAsync<AvailableThemes>("realm/themes", ct);

    Task<IReadOnlyList<RealmSigningKey>> IRealmKeysAdminService.ListAsync(CancellationToken ct)
        => GetRequiredAsync<IReadOnlyList<RealmSigningKey>>("realm-keys", ct);

    Task<AdminResult<RealmSigningKey>> IRealmKeysAdminService.GenerateAsync(AdminActor actor, GenerateRealmKeyRequest request, CancellationToken ct)
        => SendResultAsync<RealmSigningKey>(HttpMethod.Post, "realm-keys", request, ct);

    Task<AdminResult> IRealmKeysAdminService.DeleteAsync(AdminActor actor, string providerId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "realm-keys/" + Id(providerId), null, ct);

    Task<IReadOnlyList<AdminEventEntry>> IEventsAdminService.QueryAdminEventsAsync(AdminEventQuery query, CancellationToken ct)
    {
        var parameters = new List<string> { "first=" + query.First, "max=" + query.Max };
        AddQuery(parameters, "operationType", query.OperationType);
        AddQuery(parameters, "resourceType", query.ResourceType);
        AddQuery(parameters, "userId", query.UserId);
        AddQuery(parameters, "clientId", query.ClientId);
        AddQuery(parameters, "dateFrom", query.DateFrom);
        AddQuery(parameters, "dateTo", query.DateTo);
        return GetRequiredAsync<IReadOnlyList<AdminEventEntry>>("events/admin?" + string.Join('&', parameters), ct);
    }

    Task<IReadOnlyList<LoginEventEntry>> IEventsAdminService.QueryLoginEventsAsync(LoginEventQuery query, CancellationToken ct)
    {
        var parameters = new List<string> { "first=" + query.First, "max=" + query.Max };
        AddQuery(parameters, "type", query.Type);
        AddQuery(parameters, "userId", query.UserId);
        AddQuery(parameters, "clientId", query.ClientId);
        AddQuery(parameters, "dateFrom", query.DateFrom);
        AddQuery(parameters, "dateTo", query.DateTo);
        return GetRequiredAsync<IReadOnlyList<LoginEventEntry>>("events/login?" + string.Join('&', parameters), ct);
    }
}

using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.Blazor.Infrastructure.Admin;

public sealed partial class IdentityAdminApiClient
{
    Task<PagedResult<IdentityClientSummary>> IClientsAdminService.SearchAsync(AdminPageRequest request, CancellationToken ct)
        => GetRequiredAsync<PagedResult<IdentityClientSummary>>("clients" + PageQuery(request), ct);

    Task<IdentityClientDetail?> IClientsAdminService.GetAsync(string clientId, CancellationToken ct)
        => GetOrDefaultAsync<IdentityClientDetail>("clients/" + Id(clientId), ct);

    Task<AdminResult<string>> IClientsAdminService.CreateAsync(AdminActor actor, CreateClientRequest request, CancellationToken ct)
        => SendResultAsync<string>(HttpMethod.Post, "clients", request, ct);

    Task<AdminResult> IClientsAdminService.UpdateAsync(AdminActor actor, string clientId, UpdateClientRequest request, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "clients/" + Id(clientId), request, ct);

    Task<AdminResult> IClientsAdminService.DeleteAsync(AdminActor actor, string clientId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "clients/" + Id(clientId), null, ct);

    Task<AdminResult<ClientSecretResult>> IClientsAdminService.GetSecretAsync(string clientId, CancellationToken ct)
        => SendResultAsync<ClientSecretResult>(HttpMethod.Get, "clients/" + Id(clientId) + "/secret", null, ct);

    Task<AdminResult<ClientSecretResult>> IClientsAdminService.RegenerateSecretAsync(AdminActor actor, string clientId, CancellationToken ct)
        => SendResultAsync<ClientSecretResult>(HttpMethod.Post, "clients/" + Id(clientId) + "/secret/regenerate", null, ct);

    Task<AdminResult<ClientServiceAccountRoles>> IClientsAdminService.GetServiceAccountRolesAsync(string clientId, CancellationToken ct)
        => SendResultAsync<ClientServiceAccountRoles>(HttpMethod.Get, "clients/" + Id(clientId) + "/service-account/roles", null, ct);

    Task<AdminResult> IClientsAdminService.AddServiceAccountRoleAsync(AdminActor actor, string clientId, string roleName, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "clients/" + Id(clientId) + "/service-account/roles/" + Id(roleName), null, ct);

    Task<AdminResult> IClientsAdminService.RemoveServiceAccountRoleAsync(AdminActor actor, string clientId, string roleName, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "clients/" + Id(clientId) + "/service-account/roles/" + Id(roleName), null, ct);

    Task<ClientLogoutSettings?> IClientLogoutAdminService.GetAsync(string clientId, CancellationToken ct)
        => GetOrDefaultAsync<ClientLogoutSettings>("clients/" + Id(clientId) + "/logout", ct);

    Task<AdminResult> IClientLogoutAdminService.UpdateAsync(AdminActor actor, string clientId, UpdateClientLogoutSettingsRequest request, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "clients/" + Id(clientId) + "/logout", request, ct);

    Task<IReadOnlyList<IdentityClientScope>> IClientScopesAdminService.ListAsync(CancellationToken ct)
        => GetRequiredAsync<IReadOnlyList<IdentityClientScope>>("client-scopes", ct);

    Task<IdentityClientScope?> IClientScopesAdminService.GetAsync(string scopeId, CancellationToken ct)
        => GetOrDefaultAsync<IdentityClientScope>("client-scopes/" + Id(scopeId), ct);

    Task<AdminResult<string>> IClientScopesAdminService.CreateAsync(AdminActor actor, SaveClientScopeRequest request, CancellationToken ct)
        => SendResultAsync<string>(HttpMethod.Post, "client-scopes", request, ct);

    Task<AdminResult> IClientScopesAdminService.UpdateAsync(AdminActor actor, string scopeId, SaveClientScopeRequest request, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "client-scopes/" + Id(scopeId), request, ct);

    Task<AdminResult> IClientScopesAdminService.DeleteAsync(AdminActor actor, string scopeId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "client-scopes/" + Id(scopeId), null, ct);

    Task<IReadOnlyList<IdentityProtocolMapper>> IClientScopesAdminService.ListMappersAsync(string scopeId, CancellationToken ct)
        => GetRequiredAsync<IReadOnlyList<IdentityProtocolMapper>>("client-scopes/" + Id(scopeId) + "/mappers", ct);

    Task<AdminResult<string>> IClientScopesAdminService.CreateMapperAsync(AdminActor actor, string scopeId, SaveProtocolMapperRequest request, CancellationToken ct)
        => SendResultAsync<string>(HttpMethod.Post, "client-scopes/" + Id(scopeId) + "/mappers", request, ct);

    Task<AdminResult> IClientScopesAdminService.UpdateMapperAsync(AdminActor actor, string scopeId, string mapperId, SaveProtocolMapperRequest request, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "client-scopes/" + Id(scopeId) + "/mappers/" + Id(mapperId), request, ct);

    Task<AdminResult> IClientScopesAdminService.DeleteMapperAsync(AdminActor actor, string scopeId, string mapperId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "client-scopes/" + Id(scopeId) + "/mappers/" + Id(mapperId), null, ct);

    Task<AdminResult> IClientScopesAdminService.AddDefaultScopeToClientAsync(AdminActor actor, string clientId, string scopeId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "clients/" + Id(clientId) + "/default-scopes/" + Id(scopeId), null, ct);

    Task<AdminResult> IClientScopesAdminService.AddOptionalScopeToClientAsync(AdminActor actor, string clientId, string scopeId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "clients/" + Id(clientId) + "/optional-scopes/" + Id(scopeId), null, ct);

    Task<AdminResult> IClientScopesAdminService.RemoveDefaultScopeFromClientAsync(AdminActor actor, string clientId, string scopeId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "clients/" + Id(clientId) + "/default-scopes/" + Id(scopeId), null, ct);

    Task<AdminResult> IClientScopesAdminService.RemoveOptionalScopeFromClientAsync(AdminActor actor, string clientId, string scopeId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "clients/" + Id(clientId) + "/optional-scopes/" + Id(scopeId), null, ct);
}

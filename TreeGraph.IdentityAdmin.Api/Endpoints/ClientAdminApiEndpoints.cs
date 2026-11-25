using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Api.Endpoints;

public static partial class AdminApiEndpointExtensions
{
    private static void MapClientAdminApi(this RouteGroupBuilder admin)
    {
        admin.MapGet("/clients", async (
                string? search,
                int? page,
                int? pageSize,
                IClientsAdminService service,
                CancellationToken ct) =>
            Results.Ok(await service.SearchAsync(new AdminPageRequest(search, page ?? 1, pageSize ?? 25), ct)));

        admin.MapGet("/clients/{id}", async (string id, IClientsAdminService service, CancellationToken ct) =>
        {
            var client = await service.GetAsync(id, ct);
            return client is null ? Results.NotFound() : Results.Ok(client);
        });

        admin.MapPost("/clients", async (
                CreateClientRequest request,
                IClientsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.CreateAsync(actorAccessor.GetRequiredActor(), request, ct)));

        admin.MapPut("/clients/{id}", async (
                string id,
                UpdateClientRequest request,
                IClientsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.UpdateAsync(actorAccessor.GetRequiredActor(), id, request, ct)));

        admin.MapDelete("/clients/{id}", async (
                string id,
                IClientsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.DeleteAsync(actorAccessor.GetRequiredActor(), id, ct)));

        admin.MapGet("/clients/{id}/secret", async (string id, IClientsAdminService service, CancellationToken ct) =>
            ToHttpResult(await service.GetSecretAsync(id, ct)));

        admin.MapPost("/clients/{id}/secret/regenerate", async (
                string id,
                IClientsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.RegenerateSecretAsync(actorAccessor.GetRequiredActor(), id, ct)))
            .RequireStepUp();

        admin.MapGet("/clients/{id}/service-account/roles", async (
                string id,
                IClientsAdminService service,
                CancellationToken ct) =>
            ToHttpResult(await service.GetServiceAccountRolesAsync(id, ct)));

        admin.MapPut("/clients/{id}/service-account/roles/{roleName}", async (
                string id,
                string roleName,
                IClientsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.AddServiceAccountRoleAsync(actorAccessor.GetRequiredActor(), id, roleName, ct)));

        admin.MapDelete("/clients/{id}/service-account/roles/{roleName}", async (
                string id,
                string roleName,
                IClientsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.RemoveServiceAccountRoleAsync(actorAccessor.GetRequiredActor(), id, roleName, ct)));

        admin.MapGet("/clients/{id}/logout", async (
                string id,
                IClientLogoutAdminService service,
                CancellationToken ct) =>
        {
            var settings = await service.GetAsync(id, ct);
            return settings is null ? Results.NotFound() : Results.Ok(settings);
        });

        admin.MapPut("/clients/{id}/logout", async (
                string id,
                UpdateClientLogoutSettingsRequest request,
                IClientLogoutAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.UpdateAsync(actorAccessor.GetRequiredActor(), id, request, ct)));

        admin.MapGet("/client-scopes", async (IClientScopesAdminService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        admin.MapGet("/client-scopes/{scopeId}", async (
                string scopeId,
                IClientScopesAdminService service,
                CancellationToken ct) =>
        {
            var scope = await service.GetAsync(scopeId, ct);
            return scope is null ? Results.NotFound() : Results.Ok(scope);
        });

        admin.MapPost("/client-scopes", async (
                SaveClientScopeRequest request,
                IClientScopesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.CreateAsync(actorAccessor.GetRequiredActor(), request, ct)));

        admin.MapPut("/client-scopes/{scopeId}", async (
                string scopeId,
                SaveClientScopeRequest request,
                IClientScopesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.UpdateAsync(actorAccessor.GetRequiredActor(), scopeId, request, ct)));

        admin.MapDelete("/client-scopes/{scopeId}", async (
                string scopeId,
                IClientScopesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.DeleteAsync(actorAccessor.GetRequiredActor(), scopeId, ct)));

        admin.MapGet("/client-scopes/{scopeId}/mappers", async (
                string scopeId,
                IClientScopesAdminService service,
                CancellationToken ct) =>
            Results.Ok(await service.ListMappersAsync(scopeId, ct)));

        admin.MapPost("/client-scopes/{scopeId}/mappers", async (
                string scopeId,
                SaveProtocolMapperRequest request,
                IClientScopesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.CreateMapperAsync(actorAccessor.GetRequiredActor(), scopeId, request, ct)));

        admin.MapPut("/client-scopes/{scopeId}/mappers/{mapperId}", async (
                string scopeId,
                string mapperId,
                SaveProtocolMapperRequest request,
                IClientScopesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.UpdateMapperAsync(actorAccessor.GetRequiredActor(), scopeId, mapperId, request, ct)));

        admin.MapDelete("/client-scopes/{scopeId}/mappers/{mapperId}", async (
                string scopeId,
                string mapperId,
                IClientScopesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.DeleteMapperAsync(actorAccessor.GetRequiredActor(), scopeId, mapperId, ct)));

        admin.MapPut("/clients/{clientId}/default-scopes/{scopeId}", async (
                string clientId,
                string scopeId,
                IClientScopesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.AddDefaultScopeToClientAsync(actorAccessor.GetRequiredActor(), clientId, scopeId, ct)));

        admin.MapPut("/clients/{clientId}/optional-scopes/{scopeId}", async (
                string clientId,
                string scopeId,
                IClientScopesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.AddOptionalScopeToClientAsync(actorAccessor.GetRequiredActor(), clientId, scopeId, ct)));

        admin.MapDelete("/clients/{clientId}/default-scopes/{scopeId}", async (
                string clientId,
                string scopeId,
                IClientScopesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.RemoveDefaultScopeFromClientAsync(actorAccessor.GetRequiredActor(), clientId, scopeId, ct)));

        admin.MapDelete("/clients/{clientId}/optional-scopes/{scopeId}", async (
                string clientId,
                string scopeId,
                IClientScopesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.RemoveOptionalScopeFromClientAsync(actorAccessor.GetRequiredActor(), clientId, scopeId, ct)));
    }
}

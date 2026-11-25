using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Api.Endpoints;

public static partial class AdminApiEndpointExtensions
{
    private static void MapConfigurationAdminApi(this RouteGroupBuilder admin)
    {
        admin.MapGet("/dashboard/summary", async (IDashboardAdminService service, CancellationToken ct) =>
            Results.Ok(await service.GetSummaryAsync(ct)));

        admin.MapGet("/dashboard/version", async (IDashboardAdminService service, CancellationToken ct) =>
        {
            var result = await service.GetVersionAsync(ct);
            return Results.Ok(new { result.Version, result.MatchesExpected });
        });

        admin.MapGet("/identity-providers", async (IIdentityProvidersAdminService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        admin.MapGet("/identity-providers/{alias}", async (
                string alias,
                IIdentityProvidersAdminService service,
                CancellationToken ct) =>
        {
            var provider = await service.GetAsync(alias, ct);
            return provider is null ? Results.NotFound() : Results.Ok(provider);
        });

        admin.MapPost("/identity-providers", async (
                SaveIdentityProviderRequest request,
                IIdentityProvidersAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.CreateAsync(actorAccessor.GetRequiredActor(), request, ct)));

        admin.MapPut("/identity-providers/{alias}", async (
                string alias,
                SaveIdentityProviderRequest request,
                IIdentityProvidersAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.UpdateAsync(actorAccessor.GetRequiredActor(), alias, request, ct)));

        admin.MapDelete("/identity-providers/{alias}", async (
                string alias,
                IIdentityProvidersAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.DeleteAsync(actorAccessor.GetRequiredActor(), alias, ct)));

        admin.MapGet("/identity-providers/{alias}/mappers", async (
                string alias,
                IIdentityProvidersAdminService service,
                CancellationToken ct) =>
            Results.Ok(await service.ListMappersAsync(alias, ct)));

        admin.MapPost("/identity-providers/{alias}/mappers", async (
                string alias,
                IdentityProviderMapperInfo mapper,
                IIdentityProvidersAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.CreateMapperAsync(actorAccessor.GetRequiredActor(), alias, mapper, ct)));

        admin.MapDelete("/identity-providers/{alias}/mappers/{mapperId}", async (
                string alias,
                string mapperId,
                IIdentityProvidersAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.DeleteMapperAsync(actorAccessor.GetRequiredActor(), alias, mapperId, ct)));

        admin.MapGet("/user-federation", async (IFederationAdminService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        admin.MapGet("/user-federation/{componentId}", async (
                string componentId,
                IFederationAdminService service,
                CancellationToken ct) =>
        {
            var provider = await service.GetAsync(componentId, ct);
            return provider is null ? Results.NotFound() : Results.Ok(provider);
        });

        admin.MapPost("/user-federation", async (
                SaveLdapFederationRequest request,
                IFederationAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.CreateAsync(actorAccessor.GetRequiredActor(), request, ct)));

        admin.MapPut("/user-federation/{componentId}", async (
                string componentId,
                SaveLdapFederationRequest request,
                IFederationAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.UpdateAsync(actorAccessor.GetRequiredActor(), componentId, request, ct)))
            .RequireStepUp();

        admin.MapDelete("/user-federation/{componentId}", async (
                string componentId,
                IFederationAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.DeleteAsync(actorAccessor.GetRequiredActor(), componentId, ct)))
            .RequireStepUp();

        admin.MapPost("/user-federation/test-connection", async (
                SaveLdapFederationRequest request,
                IFederationAdminService service,
                CancellationToken ct) =>
            Results.Ok(await service.TestConnectionAsync(request, ct)));

        admin.MapPost("/user-federation/{componentId}/sync", async (
                string componentId,
                IFederationAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.SyncAsync(actorAccessor.GetRequiredActor(), componentId, ct)));

        admin.MapGet("/auth-flows", async (IAuthFlowsAdminService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        admin.MapGet("/auth-flows/{alias}", async (string alias, IAuthFlowsAdminService service, CancellationToken ct) =>
        {
            var flow = await service.GetAsync(alias, ct);
            return flow is null ? Results.NotFound() : Results.Ok(flow);
        });

        admin.MapPost("/auth-flows/copy", async (
                CopyAuthenticationFlowRequest request,
                IAuthFlowsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.CopyAsync(actorAccessor.GetRequiredActor(), request, ct)));

        admin.MapDelete("/auth-flows/{alias}", async (
                string alias,
                IAuthFlowsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.DeleteAsync(actorAccessor.GetRequiredActor(), alias, ct)));

        admin.MapPut("/auth-flows/{flowAlias}/executions/{executionId}", async (
                string flowAlias,
                string executionId,
                UpdateAuthenticationExecutionRequest request,
                IAuthFlowsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.UpdateExecutionAsync(
                actorAccessor.GetRequiredActor(), flowAlias, executionId, request.Requirement, ct)));

        admin.MapGet("/auth-flow-bindings", async (IAuthFlowsAdminService service, CancellationToken ct) =>
            Results.Ok(await service.ListBindingsAsync(ct)));

        admin.MapPut("/auth-flow-bindings/{bindingType}", async (
                string bindingType,
                UpdateAuthenticationFlowBindingRequest request,
                IAuthFlowsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.UpdateBindingAsync(
                actorAccessor.GetRequiredActor(), bindingType, request.FlowAlias, ct)))
            .RequireStepUp();

        admin.MapGet("/realm", async (IRealmSettingsAdminService service, CancellationToken ct) =>
        {
            var settings = await service.GetAsync(ct);
            return settings is null ? Results.NotFound() : Results.Ok(settings);
        });

        admin.MapPut("/realm", async (
                UpdateRealmSettingsRequest request,
                IRealmSettingsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.UpdateAsync(actorAccessor.GetRequiredActor(), request, ct)));

        admin.MapPut("/realm/smtp", async (
                SmtpSettingsRequest request,
                IRealmSettingsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.UpdateSmtpAsync(actorAccessor.GetRequiredActor(), request, ct)))
            .RequireStepUp();

        admin.MapGet("/realm/themes", async (IRealmSettingsAdminService service, CancellationToken ct) =>
            Results.Ok(await service.GetAvailableThemesAsync(ct)));

        admin.MapGet("/realm-keys", async (IRealmKeysAdminService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        admin.MapPost("/realm-keys", async (
                GenerateRealmKeyRequest request,
                IRealmKeysAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.GenerateAsync(actorAccessor.GetRequiredActor(), request, ct)))
            .RequireStepUp();

        admin.MapDelete("/realm-keys/{providerId}", async (
                string providerId,
                IRealmKeysAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.DeleteAsync(actorAccessor.GetRequiredActor(), providerId, ct)))
            .RequireStepUp();

        admin.MapGet("/events/admin", async (
                string? operationType,
                string? resourceType,
                string? userId,
                string? clientId,
                string? dateFrom,
                string? dateTo,
                int? first,
                int? max,
                IEventsAdminService service,
                CancellationToken ct) =>
            Results.Ok(await service.QueryAdminEventsAsync(
                new AdminEventQuery(operationType, resourceType, userId, clientId, dateFrom, dateTo, first ?? 0, max ?? 50), ct)));

        admin.MapGet("/events/login", async (
                string? type,
                string? userId,
                string? clientId,
                string? dateFrom,
                string? dateTo,
                int? first,
                int? max,
                IEventsAdminService service,
                CancellationToken ct) =>
            Results.Ok(await service.QueryLoginEventsAsync(
                new LoginEventQuery(type, userId, clientId, dateFrom, dateTo, first ?? 0, max ?? 50), ct)));

        admin.MapGet("/audit", async (
                string? actorSub,
                string? action,
                string? targetType,
                string? result,
                DateTimeOffset? from,
                DateTimeOffset? to,
                int? page,
                int? pageSize,
                IAdminAuditQueryService service,
                CancellationToken ct) =>
            Results.Ok(await service.QueryAsync(
                new AdminAuditQuery(actorSub, action, targetType, result, from, to, page ?? 1, pageSize ?? 50), ct)));

        admin.MapPost("/export", async (
                RealmExportOptions options,
                IExportAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.ExportAsync(actorAccessor.GetRequiredActor(), options, ct)));
    }
}

internal sealed record UpdateAuthenticationExecutionRequest(string Requirement);
internal sealed record UpdateAuthenticationFlowBindingRequest(string FlowAlias);

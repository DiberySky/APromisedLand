using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Api.Endpoints;

public static partial class AdminApiEndpointExtensions
{
    private static void MapDirectoryAdminApi(this RouteGroupBuilder admin)
    {
        admin.MapPost("/users", async (
                CreateUserRequest request,
                IUsersAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.CreateAsync(actorAccessor.GetRequiredActor(), request, ct)));

        admin.MapPut("/users/{id}", async (
                string id,
                UpdateUserRequest request,
                IUsersAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.UpdateAsync(actorAccessor.GetRequiredActor(), id, request, ct)));

        admin.MapPost("/users/{id}/reset-password", async (
                string id,
                ResetPasswordRequest request,
                IUsersAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.ResetPasswordAsync(actorAccessor.GetRequiredActor(), id, request, ct)))
            .RequireStepUp();

        admin.MapPost("/users/{id}/sessions/logout", async (
                string id,
                IUsersAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.LogoutSessionsAsync(actorAccessor.GetRequiredActor(), id, ct)));

        admin.MapGet("/users/{userId}/sessions", async (
                string userId,
                ISessionsAdminService service,
                CancellationToken ct) =>
            Results.Ok(await service.ListUserSessionsAsync(userId, ct)));

        admin.MapDelete("/users/{userId}/sessions/{sessionId}", async (
                string userId,
                string sessionId,
                ISessionsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.LogoutSessionAsync(actorAccessor.GetRequiredActor(), userId, sessionId, ct)));

        admin.MapDelete("/users/{userId}/sessions", async (
                string userId,
                ISessionsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.LogoutAllUserSessionsAsync(actorAccessor.GetRequiredActor(), userId, ct)));

        admin.MapGet("/groups", async (bool? brief, IGroupsAdminService service, CancellationToken ct) =>
            Results.Ok(await service.GetTreeAsync(brief ?? true, ct)));

        admin.MapGet("/groups/{id}", async (string id, IGroupsAdminService service, CancellationToken ct) =>
        {
            var group = await service.GetAsync(id, ct);
            return group is null ? Results.NotFound() : Results.Ok(group);
        });

        admin.MapPost("/groups", async (
                CreateGroupRequest request,
                IGroupsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.CreateAsync(actorAccessor.GetRequiredActor(), request, ct)));

        admin.MapPut("/groups/{id}", async (
                string id,
                UpdateGroupRequest request,
                IGroupsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.UpdateAsync(actorAccessor.GetRequiredActor(), id, request, ct)));

        admin.MapDelete("/groups/{id}", async (
                string id,
                IGroupsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.DeleteAsync(actorAccessor.GetRequiredActor(), id, ct)))
            .RequireStepUp();

        admin.MapGet("/groups/{id}/members", async (string id, IGroupsAdminService service, CancellationToken ct) =>
            Results.Ok(await service.ListMembersAsync(id, ct)));

        admin.MapPut("/groups/{id}/members/{userId}", async (
                string id,
                string userId,
                IGroupsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.AddMemberAsync(actorAccessor.GetRequiredActor(), id, userId, ct)));

        admin.MapDelete("/groups/{id}/members/{userId}", async (
                string id,
                string userId,
                IGroupsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.RemoveMemberAsync(actorAccessor.GetRequiredActor(), id, userId, ct)));

        admin.MapPost("/groups/spaces/{spaceId}", async (
                string spaceId,
                IGroupsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.EnsureSpaceGroupAsync(actorAccessor.GetRequiredActor(), spaceId, ct)));

        admin.MapGet("/roles", async (IRolesAdminService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        admin.MapGet("/roles/{roleName}", async (string roleName, IRolesAdminService service, CancellationToken ct) =>
        {
            var role = await service.GetAsync(roleName, ct);
            return role is null ? Results.NotFound() : Results.Ok(role);
        });

        admin.MapPost("/roles", async (
                CreateRoleRequest request,
                IRolesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.CreateAsync(actorAccessor.GetRequiredActor(), request, ct)));

        admin.MapPut("/roles/{roleName}", async (
                string roleName,
                UpdateRoleRequest request,
                IRolesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.UpdateAsync(actorAccessor.GetRequiredActor(), roleName, request, ct)));

        admin.MapDelete("/roles/{roleName}", async (
                string roleName,
                IRolesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.DeleteAsync(actorAccessor.GetRequiredActor(), roleName, ct)));

        admin.MapGet("/roles/{roleName}/members", async (string roleName, IRolesAdminService service, CancellationToken ct) =>
            Results.Ok(await service.ListMembersAsync(roleName, ct)));

        admin.MapPut("/roles/{roleName}/members/{userId}", async (
                string roleName,
                string userId,
                IRolesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.AddMemberAsync(actorAccessor.GetRequiredActor(), roleName, userId, ct)));

        admin.MapDelete("/roles/{roleName}/members/{userId}", async (
                string roleName,
                string userId,
                IRolesAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.RemoveMemberAsync(actorAccessor.GetRequiredActor(), roleName, userId, ct)));

        admin.MapGet("/organizations", async (
                string? search,
                int? page,
                int? pageSize,
                IOrganizationsAdminService service,
                CancellationToken ct) =>
            Results.Ok(await service.SearchAsync(new AdminPageRequest(search, page ?? 1, pageSize ?? 25), ct)));

        admin.MapGet("/organizations/{id}", async (string id, IOrganizationsAdminService service, CancellationToken ct) =>
        {
            var organization = await service.GetAsync(id, ct);
            return organization is null ? Results.NotFound() : Results.Ok(organization);
        });

        admin.MapPost("/organizations", async (
                CreateOrganizationRequest request,
                IOrganizationsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.CreateAsync(actorAccessor.GetRequiredActor(), request, ct)));

        admin.MapPut("/organizations/{id}", async (
                string id,
                UpdateOrganizationRequest request,
                IOrganizationsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.UpdateAsync(actorAccessor.GetRequiredActor(), id, request, ct)));

        admin.MapDelete("/organizations/{id}", async (
                string id,
                IOrganizationsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.DeleteAsync(actorAccessor.GetRequiredActor(), id, ct)));

        admin.MapGet("/organizations/{id}/members", async (
                string id,
                IOrganizationsAdminService service,
                CancellationToken ct) =>
            Results.Ok(await service.ListMembersAsync(id, ct)));

        admin.MapPut("/organizations/{id}/members/{userId}", async (
                string id,
                string userId,
                IOrganizationsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.AddMemberAsync(actorAccessor.GetRequiredActor(), id, userId, ct)));

        admin.MapDelete("/organizations/{id}/members/{userId}", async (
                string id,
                string userId,
                IOrganizationsAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.RemoveMemberAsync(actorAccessor.GetRequiredActor(), id, userId, ct)));
    }
}

using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.Blazor.Infrastructure.Admin;

public sealed partial class IdentityAdminApiClient
{
    Task<IReadOnlyList<IdentityGroup>> IGroupsAdminService.GetTreeAsync(bool brief, CancellationToken ct)
        => GetRequiredAsync<IReadOnlyList<IdentityGroup>>("groups?brief=" + brief.ToString().ToLowerInvariant(), ct);

    Task<IdentityGroup?> IGroupsAdminService.GetAsync(string groupId, CancellationToken ct)
        => GetOrDefaultAsync<IdentityGroup>("groups/" + Id(groupId), ct);

    Task<AdminResult<string>> IGroupsAdminService.CreateAsync(AdminActor actor, CreateGroupRequest request, CancellationToken ct)
        => SendResultAsync<string>(HttpMethod.Post, "groups", request, ct);

    Task<AdminResult> IGroupsAdminService.UpdateAsync(AdminActor actor, string groupId, UpdateGroupRequest request, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "groups/" + Id(groupId), request, ct);

    Task<AdminResult> IGroupsAdminService.DeleteAsync(AdminActor actor, string groupId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "groups/" + Id(groupId), null, ct);

    Task<IReadOnlyList<IdentityUserSummary>> IGroupsAdminService.ListMembersAsync(string groupId, CancellationToken ct)
        => GetRequiredAsync<IReadOnlyList<IdentityUserSummary>>("groups/" + Id(groupId) + "/members", ct);

    Task<AdminResult> IGroupsAdminService.AddMemberAsync(AdminActor actor, string groupId, string userId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "groups/" + Id(groupId) + "/members/" + Id(userId), null, ct);

    Task<AdminResult> IGroupsAdminService.RemoveMemberAsync(AdminActor actor, string groupId, string userId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "groups/" + Id(groupId) + "/members/" + Id(userId), null, ct);

    Task<AdminResult<string>> IGroupsAdminService.EnsureSpaceGroupAsync(AdminActor actor, string spaceId, CancellationToken ct)
        => SendResultAsync<string>(HttpMethod.Post, "groups/spaces/" + Id(spaceId), null, ct);

    Task<IReadOnlyList<IdentityRole>> IRolesAdminService.ListAsync(CancellationToken ct)
        => GetRequiredAsync<IReadOnlyList<IdentityRole>>("roles", ct);

    Task<IdentityRole?> IRolesAdminService.GetAsync(string roleName, CancellationToken ct)
        => GetOrDefaultAsync<IdentityRole>("roles/" + Id(roleName), ct);

    Task<AdminResult<string>> IRolesAdminService.CreateAsync(AdminActor actor, CreateRoleRequest request, CancellationToken ct)
        => SendResultAsync<string>(HttpMethod.Post, "roles", request, ct);

    Task<AdminResult> IRolesAdminService.UpdateAsync(AdminActor actor, string roleName, UpdateRoleRequest request, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "roles/" + Id(roleName), request, ct);

    Task<AdminResult> IRolesAdminService.DeleteAsync(AdminActor actor, string roleName, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "roles/" + Id(roleName), null, ct);

    Task<IReadOnlyList<IdentityUserSummary>> IRolesAdminService.ListMembersAsync(string roleName, CancellationToken ct)
        => GetRequiredAsync<IReadOnlyList<IdentityUserSummary>>("roles/" + Id(roleName) + "/members", ct);

    Task<AdminResult> IRolesAdminService.AddMemberAsync(AdminActor actor, string roleName, string userId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "roles/" + Id(roleName) + "/members/" + Id(userId), null, ct);

    Task<AdminResult> IRolesAdminService.RemoveMemberAsync(AdminActor actor, string roleName, string userId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "roles/" + Id(roleName) + "/members/" + Id(userId), null, ct);

    Task<PagedResult<OrganizationSummary>> IOrganizationsAdminService.SearchAsync(AdminPageRequest request, CancellationToken ct)
        => GetRequiredAsync<PagedResult<OrganizationSummary>>("organizations" + PageQuery(request), ct);

    Task<OrganizationDetail?> IOrganizationsAdminService.GetAsync(string organizationId, CancellationToken ct)
        => GetOrDefaultAsync<OrganizationDetail>("organizations/" + Id(organizationId), ct);

    Task<AdminResult<string>> IOrganizationsAdminService.CreateAsync(AdminActor actor, CreateOrganizationRequest request, CancellationToken ct)
        => SendResultAsync<string>(HttpMethod.Post, "organizations", request, ct);

    Task<AdminResult> IOrganizationsAdminService.UpdateAsync(AdminActor actor, string organizationId, UpdateOrganizationRequest request, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "organizations/" + Id(organizationId), request, ct);

    Task<AdminResult> IOrganizationsAdminService.DeleteAsync(AdminActor actor, string organizationId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "organizations/" + Id(organizationId), null, ct);

    Task<IReadOnlyList<OrganizationMember>> IOrganizationsAdminService.ListMembersAsync(string organizationId, CancellationToken ct)
        => GetRequiredAsync<IReadOnlyList<OrganizationMember>>("organizations/" + Id(organizationId) + "/members", ct);

    Task<AdminResult> IOrganizationsAdminService.AddMemberAsync(AdminActor actor, string organizationId, string userId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Put, "organizations/" + Id(organizationId) + "/members/" + Id(userId), null, ct);

    Task<AdminResult> IOrganizationsAdminService.RemoveMemberAsync(AdminActor actor, string organizationId, string userId, CancellationToken ct)
        => SendResultAsync(HttpMethod.Delete, "organizations/" + Id(organizationId) + "/members/" + Id(userId), null, ct);
}

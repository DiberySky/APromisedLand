namespace TreeGraph.Shared.Identity.Admin;

/// <summary>组织列表项。</summary>
public sealed record OrganizationSummary(
    string Id,
    string? Name,
    string? Alias,
    bool Enabled,
    string? Description,
    int DomainCount);

/// <summary>组织详情。</summary>
public sealed record OrganizationDetail(
    string Id,
    string? Name,
    string? Alias,
    bool Enabled,
    string? Description,
    string? RedirectUrl,
    IReadOnlyList<string> Domains,
    IReadOnlyDictionary<string, string> Attributes);

/// <summary>创建组织请求。</summary>
public sealed record CreateOrganizationRequest(
    string Name,
    string Alias,
    bool Enabled = true,
    string? Description = null,
    string? RedirectUrl = null,
    IReadOnlyList<string>? Domains = null,
    IReadOnlyDictionary<string, string>? Attributes = null);

/// <summary>更新组织请求。null 表示不修改；Alias 不可变更。</summary>
public sealed record UpdateOrganizationRequest(
    string? Name = null,
    bool? Enabled = null,
    string? Description = null,
    string? RedirectUrl = null,
    IReadOnlyList<string>? Domains = null,
    IReadOnlyDictionary<string, string>? Attributes = null);

/// <summary>组织成员。</summary>
public sealed record OrganizationMember(
    string UserId,
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName);

/// <summary>组织管理服务（Keycloak 26 Organizations）。</summary>
public interface IOrganizationsAdminService
{
    Task<PagedResult<OrganizationSummary>> SearchAsync(AdminPageRequest request, CancellationToken ct = default);

    Task<OrganizationDetail?> GetAsync(string organizationId, CancellationToken ct = default);

    Task<AdminResult<string>> CreateAsync(
        AdminActor actor,
        CreateOrganizationRequest request,
        CancellationToken ct = default);

    Task<AdminResult> UpdateAsync(
        AdminActor actor,
        string organizationId,
        UpdateOrganizationRequest request,
        CancellationToken ct = default);

    Task<AdminResult> DeleteAsync(AdminActor actor, string organizationId, CancellationToken ct = default);

    Task<IReadOnlyList<OrganizationMember>> ListMembersAsync(string organizationId, CancellationToken ct = default);

    Task<AdminResult> AddMemberAsync(AdminActor actor, string organizationId, string userId, CancellationToken ct = default);

    Task<AdminResult> RemoveMemberAsync(AdminActor actor, string organizationId, string userId, CancellationToken ct = default);
}

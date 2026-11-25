namespace TreeGraph.Shared.Identity.Admin;

/// <summary>realm 角色。</summary>
public sealed record IdentityRole(
    string Id,
    string Name,
    string? Description,
    bool Composite,
    bool BuiltIn = false);

/// <summary>创建角色请求。</summary>
public sealed record CreateRoleRequest(string Name, string? Description = null, bool Composite = false);

/// <summary>更新角色请求。</summary>
public sealed record UpdateRoleRequest(string? Description = null, string? NewName = null);

/// <summary>角色管理服务。</summary>
public interface IRolesAdminService
{
    Task<IReadOnlyList<IdentityRole>> ListAsync(CancellationToken ct = default);

    Task<IdentityRole?> GetAsync(string roleName, CancellationToken ct = default);

    Task<AdminResult<string>> CreateAsync(AdminActor actor, CreateRoleRequest request, CancellationToken ct = default);

    Task<AdminResult> UpdateAsync(AdminActor actor, string roleName, UpdateRoleRequest request, CancellationToken ct = default);

    Task<AdminResult> DeleteAsync(AdminActor actor, string roleName, CancellationToken ct = default);

    Task<IReadOnlyList<IdentityUserSummary>> ListMembersAsync(string roleName, CancellationToken ct = default);

    Task<AdminResult> AddMemberAsync(AdminActor actor, string roleName, string userId, CancellationToken ct = default);

    Task<AdminResult> RemoveMemberAsync(AdminActor actor, string roleName, string userId, CancellationToken ct = default);
}

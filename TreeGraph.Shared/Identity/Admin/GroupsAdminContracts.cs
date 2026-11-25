namespace TreeGraph.Shared.Identity.Admin;

/// <summary>组节点。</summary>
public sealed record IdentityGroup(
    string Id,
    string Name,
    string Path,
    string? Description,
    int? MemberCount,
    IReadOnlyList<IdentityGroup> Children)
{
    /// <summary>是否为空间组（<c>space-</c> 前缀）。</summary>
    public bool IsSpaceGroup => Name.StartsWith(IdentityConstants.SpaceGroupPrefix, StringComparison.Ordinal);
}

/// <summary>创建组请求。ParentId 为空表示顶层组。</summary>
public sealed record CreateGroupRequest(string Name, string? ParentId = null, string? Description = null);

/// <summary>更新组请求。</summary>
public sealed record UpdateGroupRequest(string? Name = null, string? Description = null);

/// <summary>组管理服务。</summary>
public interface IGroupsAdminService
{
    /// <summary>读取组树。</summary>
    Task<IReadOnlyList<IdentityGroup>> GetTreeAsync(bool brief = true, CancellationToken ct = default);

    Task<IdentityGroup?> GetAsync(string groupId, CancellationToken ct = default);

    Task<AdminResult<string>> CreateAsync(AdminActor actor, CreateGroupRequest request, CancellationToken ct = default);

    Task<AdminResult> UpdateAsync(AdminActor actor, string groupId, UpdateGroupRequest request, CancellationToken ct = default);

    /// <summary>删除组。空间组不允许从此入口删除。</summary>
    Task<AdminResult> DeleteAsync(AdminActor actor, string groupId, CancellationToken ct = default);

    Task<IReadOnlyList<IdentityUserSummary>> ListMembersAsync(string groupId, CancellationToken ct = default);

    Task<AdminResult> AddMemberAsync(AdminActor actor, string groupId, string userId, CancellationToken ct = default);

    Task<AdminResult> RemoveMemberAsync(AdminActor actor, string groupId, string userId, CancellationToken ct = default);

    /// <summary>幂等创建 <c>/spaces/space-{spaceId}</c> 组，返回组 ID。</summary>
    Task<AdminResult<string>> EnsureSpaceGroupAsync(AdminActor actor, string spaceId, CancellationToken ct = default);
}

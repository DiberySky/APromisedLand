using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Services;

namespace TreeGraph.Api.TreeEavSky.Services;

/// <summary>
/// 单位树节点 iNode 宿主守卫：inodeId 是真实单位树节点时认领。
/// </summary>
public sealed class UnitTreeInodeGuard(TreeGraphDbContext db) : IInodeOwnerGuard
{
    public async Task<bool> NodeExistsAsync(
        string inodeId, CancellationToken ct = default)
        => await db.UnitTrees.AnyAsync(u => u.Id == inodeId, ct);
}

/// <summary>
/// 分类树节点 iNode 宿主守卫：inodeId 是真实分类树节点时认领。
/// </summary>
public sealed class CategoryTreeInodeGuard(TreeGraphDbContext db) : IInodeOwnerGuard
{
    public async Task<bool> NodeExistsAsync(
        string inodeId, CancellationToken ct = default)
        => await db.CategoryTrees.AnyAsync(c => c.Id == inodeId, ct);
}

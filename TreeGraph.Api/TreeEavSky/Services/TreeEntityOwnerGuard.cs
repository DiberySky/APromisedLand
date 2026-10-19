using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Services;

namespace TreeGraph.Api.TreeEavSky.Services;

/// <summary>
/// 单位树节点 iNode 宿主守卫：inodeId 是真实单位树节点时认领。
/// </summary>
public sealed class UnitTreeInodeGuard(TreeGraphDbContext db) : IInodeOwnerGuard
{
    public string NodeType => "UnitTree";

    public async Task<bool> NodeExistsAsync(
        string inodeId, CancellationToken ct = default)
        => await db.UnitTrees.AnyAsync(u => u.Id == inodeId, ct);

    // 展示文本与 UnitTree.Text() 一致：Name，或带简称的 "Name 【简称】"
    public async Task<Dictionary<string, string>> GetNodeNamesAsync(
        IReadOnlyCollection<string> inodeIds, CancellationToken ct = default)
        => (await db.UnitTrees
                .AsNoTracking()
                .Where(u => inodeIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name, u.Abbreviation })
                .ToListAsync(ct))
            .ToDictionary(
                u => u.Id,
                u => string.IsNullOrEmpty(u.Abbreviation)
                    ? u.Name
                    : $"{u.Name} 【{u.Abbreviation}】",
                StringComparer.Ordinal);
}

/// <summary>
/// 分类树节点 iNode 宿主守卫：inodeId 是真实分类树节点时认领。
/// </summary>
public sealed class CategoryTreeInodeGuard(TreeGraphDbContext db) : IInodeOwnerGuard
{
    public string NodeType => "CategoryTree";

    public async Task<bool> NodeExistsAsync(
        string inodeId, CancellationToken ct = default)
        => await db.CategoryTrees.AnyAsync(c => c.Id == inodeId, ct);

    // 展示文本与 CategoryTree.Text() 一致：Name
    public async Task<Dictionary<string, string>> GetNodeNamesAsync(
        IReadOnlyCollection<string> inodeIds, CancellationToken ct = default)
        => await db.CategoryTrees
            .AsNoTracking()
            .Where(c => inodeIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, StringComparer.Ordinal, ct);
}

/// <summary>
/// 空间树节点 iNode 宿主守卫：inodeId 是真实空间树节点时认领。
/// </summary>
public sealed class SpaceTreeInodeGuard(TreeGraphDbContext db) : IInodeOwnerGuard
{
    public string NodeType => "SpaceTree";

    public async Task<bool> NodeExistsAsync(
        string inodeId, CancellationToken ct = default)
        => await db.SpaceTrees.AnyAsync(c => c.Id == inodeId, ct);

    // 展示文本与 SpaceTree.Text() 一致：Name
    public async Task<Dictionary<string, string>> GetNodeNamesAsync(
        IReadOnlyCollection<string> inodeIds, CancellationToken ct = default)
        => await db.SpaceTrees
            .AsNoTracking()
            .Where(s => inodeIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, StringComparer.Ordinal, ct);
}

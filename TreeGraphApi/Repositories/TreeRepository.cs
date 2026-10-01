using Microsoft.EntityFrameworkCore;
using Npgsql;
using TreeGraphApi.Data;
using TreeGraphApi.Entities;

namespace TreeGraphApi.Repositories;

public class TreeRepository : ITreeRepository
{
    private readonly TreeDbContext _context;
    private DbSet<TreeNodeEntity> Set => _context.TreeNodes;

    public TreeRepository(TreeDbContext context) => _context = context;

    public async Task<IReadOnlyCollection<TreeNodeEntity>> GetRootsAsync(int skip, int take, CancellationToken ct = default)
        => await Set.Where(x => x.ParentId == null)
                    .OrderBy(x => x.SortOrder)
                    .Skip(skip).Take(take)
                    .ToListAsync(ct);

    public async Task<IReadOnlyCollection<TreeNodeEntity>> GetChildrenAsync(string parentId, CancellationToken ct = default)
        => await Set.Where(x => x.ParentId == parentId)
                    .OrderBy(x => x.SortOrder)
                    .ToListAsync(ct);

    public async Task<TreeNodeEntity?> GetByIdAsync(string id, CancellationToken ct = default)
        => await Set.FindAsync(new object[] { id }, ct);

    /// <summary>
    /// 子树查询:用递归 CTE 实现,纯标准 SQL,不依赖 ltree。
    /// 排序按 Path 深度优先。
    /// </summary>
    public async Task<IReadOnlyCollection<TreeNodeEntity>> GetSubtreeAsync(string rootId, CancellationToken ct = default)
    {
        const string sql = @"
            WITH RECURSIVE subtree AS (
                SELECT * FROM tree_nodes WHERE id = @rootId
                UNION ALL
                SELECT t.* FROM tree_nodes t
                INNER JOIN subtree s ON t.parent_id = s.id
            )
            SELECT * FROM subtree ORDER BY path";

        return await Set.FromSqlRaw(sql,
            new NpgsqlParameter("rootId", rootId)).ToListAsync(ct);
    }

    /// <summary>
    /// 祖先链查询:递归 CTE 自底向上回溯 ParentId。
    /// </summary>
    public async Task<IReadOnlyCollection<TreeNodeEntity>> GetAncestorsAsync(string nodeId, CancellationToken ct = default)
    {
        const string sql = @"
            WITH RECURSIVE ancestors AS (
                SELECT * FROM tree_nodes WHERE id = @nodeId
                UNION ALL
                SELECT t.* FROM tree_nodes t
                INNER JOIN ancestors a ON t.id = a.parent_id
            )
            SELECT * FROM ancestors WHERE id != @nodeId ORDER BY path";

        return await Set.FromSqlRaw(sql,
            new NpgsqlParameter("nodeId", nodeId)).ToListAsync(ct);
    }

    public async Task<TreeNodeEntity> AddAsync(TreeNodeEntity node, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(node.Id))
            node.Id = Guid.NewGuid().ToString();

        if (node.ParentId is null)
        {
            node.Path = node.Id;
        }
        else
        {
            var parent = await GetByIdAsync(node.ParentId, ct)
                ?? throw new InvalidOperationException($"Parent {node.ParentId} not found");
            node.Path = $"{parent.Path}.{node.Id}";

            // 同步父节点 Children 导航集合 + HasChildren 标记
            parent.Children.Add(node);
            parent.HasChildren = true;
        }

        node.CreatedAt = node.UpdatedAt = DateTimeOffset.UtcNow;
        await Set.AddAsync(node, ct);
        await _context.SaveChangesAsync(ct);
        return node;
    }

    public async Task<TreeNodeEntity> UpdateAsync(TreeNodeEntity node, CancellationToken ct = default)
    {
        node.UpdatedAt = DateTimeOffset.UtcNow;
        Set.Update(node);
        await _context.SaveChangesAsync(ct);
        return node;
    }

    /// <summary>
    /// 优化 #6:MoveAsync 同步更新 Children 导航集合 + HasChildren 标记,
    /// 并用 LIKE 前缀匹配批量重写所有后代的 Path 前缀。
    /// </summary>
    public async Task MoveAsync(string nodeId, string newParentId, CancellationToken ct = default)
    {
        var node = await GetByIdAsync(nodeId, ct)
            ?? throw new InvalidOperationException($"Node {nodeId} not found");
        var oldParentId = node.ParentId;
        var oldPath = node.Path;

        var newParent = await GetByIdAsync(newParentId, ct)
            ?? throw new InvalidOperationException($"New parent {newParentId} not found");

        var newPath = $"{newParent.Path}.{node.Id}";

        // 1. 批量更新所有后代节点的 Path 前缀(用 LIKE 匹配以 oldPath 开头的路径)
        //    新前缀长度与旧前缀长度之差用于 substring 起始位置,这里直接用字符串替换更直观
        await _context.Database.ExecuteSqlRawAsync(@"
            UPDATE tree_nodes
            SET path = @newPrefix || substring(path, length(@oldPrefix) + 1)
            WHERE path = @oldPrefix OR path LIKE @oldPrefix || '.%'",
            new NpgsqlParameter("newPrefix", newPath),
            new NpgsqlParameter("oldPrefix", oldPath), ct);

        // 2. 同步当前节点 + 新旧父节点导航属性
        node.ParentId = newParentId;
        node.Path = newPath;
        node.UpdatedAt = DateTimeOffset.UtcNow;

        newParent.Children.Add(node);
        newParent.HasChildren = true;

        if (oldParentId is not null)
        {
            var oldParent = await GetByIdAsync(oldParentId, ct);
            if (oldParent is not null)
            {
                oldParent.Children.Remove(node);
                oldParent.HasChildren = oldParent.Children.Count > 0;
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 删除子树:用 LIKE 前缀匹配删除所有后代,再用 EF 删除当前节点。
    /// </summary>
    public async Task DeleteSubtreeAsync(string nodeId, CancellationToken ct = default)
    {
        var node = await GetByIdAsync(nodeId, ct)
            ?? throw new InvalidOperationException($"Node {nodeId} not found");
        var subtreePath = node.Path;

        // 先删后代:Path 等于子树路径,或以 "subtreePath." 开头
        await _context.Database.ExecuteSqlRawAsync(@"
            DELETE FROM tree_nodes
            WHERE path = @subtreePath OR path LIKE @subtreePath || '.%'",
            new NpgsqlParameter("subtreePath", subtreePath), ct);

        // 同步父节点 Children 导航集合 + HasChildren 标记
        if (node.ParentId is not null)
        {
            var parent = await GetByIdAsync(node.ParentId, ct);
            if (parent is not null)
            {
                parent.Children.Remove(node);
                parent.HasChildren = parent.Children.Count > 0;
            }
        }

        // 再删自身
        Set.Remove(node);
        await _context.SaveChangesAsync(ct);
    }
}

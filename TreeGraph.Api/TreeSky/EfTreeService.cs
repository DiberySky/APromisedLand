using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Blazor.Shared.Trees.TreeSky.Models;

namespace TreeGraph.Api.TreeSky;

/// <summary>
/// 泛型树 EF Core 实现（合并自源 CategoryTreeService / UnitTreeService，两者 95% 重复）。
/// 差异点（DbSet、名称字段）由泛型约束与接口消除：
/// 实体只需实现 <see cref="ITreeNodeBase{T}"/> 并在 EavDbContext 注册即可。
/// 与源实现的行为差异：
/// - HasChildren 不落库，读取时按子表实时计算（避免源实现中 HasChildren 与真实子节点漂移的问题）；
/// - 防环检测用迭代走父链替代 Npgsql 递归 CTE，与具体表名解耦；
/// - 移动节点到自身视为非法（源 CTE 判定 ancestorId == nodeId 时放行，会形成自环）。
/// </summary>
public class EfTreeService<TNode>(EavDbContext db) : ITreeService<TNode>
    where TNode : class, ITreeNodeBase<TNode>, new()
{
    private DbSet<TNode> Set => db.Set<TNode>();

    // ==================== 查询 ====================

    public async Task<IReadOnlyList<TreeNodeDto<TNode>>> GetRootNodesAsync(
        string? rootId = null, CancellationToken cancellationToken = default)
    {
        List<TNode> entities;
        if (!string.IsNullOrEmpty(rootId))
        {
            var root = await Set.FirstOrDefaultAsync(c => c.Id == rootId, cancellationToken);
            entities = root == null ? [] : [root];
        }
        else
        {
            entities = await Set
                .Where(c => c.ParentId == null)
                .OrderBy(c => c.SortOrder)
                .ToListAsync(cancellationToken);
        }

        return await ToLazyDtosAsync(entities, cancellationToken);
    }

    public async Task<IReadOnlyList<TreeNodeDto<TNode>>> GetChildrenAsync(
        string parentId, CancellationToken cancellationToken = default)
    {
        var entities = await Set
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);

        return await ToLazyDtosAsync(entities, cancellationToken);
    }

    public async Task<IReadOnlyList<TreeNodeDto<TNode>>> QueryNodesAsync(
        TreeQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        var query = Set.AsQueryable();

        if (!string.IsNullOrEmpty(queryParams.ParentId))
            query = query.Where(c => c.ParentId == queryParams.ParentId);

        // 注意：SearchTerm / OnlyWithChildren 依赖具体字段，泛型层不支持，
        // 由宿主在具体控制器中扩展（参考源 CategoryTreeController 的 search 端点）。

        var entities = await query
            .OrderBy(c => c.SortOrder)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync(cancellationToken);

        return await ToLazyDtosAsync(entities, cancellationToken);
    }

    public async Task<TreeNodeDto<TNode>?> GetFullTreeAsync(
        string? rootId = null, CancellationToken cancellationToken = default)
    {
        var all = await Set.OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);
        if (all.Count == 0)
            return null;

        var root = string.IsNullOrEmpty(rootId)
            ? all.FirstOrDefault(n => n.ParentId == null)
            : all.FirstOrDefault(n => n.Id == rootId);
        if (root == null)
            return null;

        var childrenLookup = all
            .Where(n => n.ParentId != null)
            .GroupBy(n => n.ParentId!)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.SortOrder).ToList());

        return BuildDto(root);

        TreeNodeDto<TNode> BuildDto(TNode entity)
        {
            var children = childrenLookup.GetValueOrDefault(entity.Id) ?? [];
            return new TreeNodeDto<TNode>
            {
                Id = entity.Id,
                ParentId = entity.ParentId,
                Value = entity,
                Text = entity.Text(),
                SortOrder = entity.SortOrder,
                HasChildren = children.Count > 0,
                Children = children.Count > 0 ? children.Select(BuildDto).ToList() : null,
            };
        }
    }

    public async Task<IReadOnlyList<string>> GetAncestorPathAsync(
        string nodeId, CancellationToken cancellationToken = default)
    {
        var path = new List<string>();
        var currentId = nodeId;

        while (!string.IsNullOrEmpty(currentId))
        {
            var node = await Set
                .Where(c => c.Id == currentId)
                .Select(c => new { c.Id, c.ParentId })
                .FirstOrDefaultAsync(cancellationToken);

            if (node == null) break;

            path.Insert(0, node.Id);
            currentId = node.ParentId;
        }

        return path.AsReadOnly();
    }

    // ==================== 写入 ====================

    public async Task<TreeNodeDto<TNode>> CreateNodeAsync(
        TreeNodeDto<TNode> nodeDto, CancellationToken cancellationToken = default)
    {
        var entity = nodeDto.Value ?? new TNode();
        if (string.IsNullOrWhiteSpace(entity.Id))
            entity.Id = Guid.NewGuid().ToString("N");
        entity.ParentId = nodeDto.ParentId;
        entity.SortOrder = nodeDto.Value?.SortOrder ?? nodeDto.SortOrder;

        await Set.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return await ToLazyDtoAsync(entity, cancellationToken);
    }

    public async Task<TreeNodeDto<TNode>> UpdateNodeAsync(
        TreeNodeDto<TNode> nodeDto, CancellationToken cancellationToken = default)
    {
        var entity = await Set.FindAsync([nodeDto.Id], cancellationToken)
            ?? throw new KeyNotFoundException($"节点 {nodeDto.Id} 不存在");

        if (nodeDto.Value is { } value)
        {
            // 整体拷贝标量字段（含接口未暴露的 Name 等具体属性）；
            // ParentId 不在此修改——移动只走 MoveNodeAsync。
            var originalParentId = entity.ParentId;
            db.Entry(entity).CurrentValues.SetValues(value);
            entity.Id = nodeDto.Id;
            entity.ParentId = originalParentId;
        }
        else
        {
            entity.SortOrder = nodeDto.SortOrder;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ToLazyDtoAsync(entity, cancellationToken);
    }

    public async Task<TreeNodeDto<TNode>> UpdateChildrenAsync(
        TreeNodeDto<TNode> nodeDto, CancellationToken cancellationToken = default)
    {
        var children = await Set
            .Where(c => c.ParentId == nodeDto.Id)
            .ToListAsync(cancellationToken);

        // 按传入 Children 顺序重排 SortOrder（与前端排序对话框语义一致）
        var order = 0;
        foreach (var childDto in nodeDto.Children ?? [])
        {
            var childId = childDto.Value?.Id ?? childDto.Id;
            var child = children.FirstOrDefault(c => c.Id == childId);
            if (child != null)
                child.SortOrder = order;
            order++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return nodeDto;
    }

    public async Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var node = await Set.FirstOrDefaultAsync(c => c.Id == nodeId, cancellationToken);
        if (node == null)
            return false;

        // 级联硬删除全部后代
        var all = await Set.ToListAsync(cancellationToken);
        var byParent = all.Where(n => n.ParentId != null)
            .GroupBy(n => n.ParentId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var toDelete = new List<TNode>();
        var stack = new Stack<TNode>();
        stack.Push(node);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            toDelete.Add(current);
            foreach (var child in byParent.GetValueOrDefault(current.Id) ?? [])
                stack.Push(child);
        }

        Set.RemoveRange(toDelete);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MoveNodeAsync(
        string nodeId, string? newParentId, CancellationToken cancellationToken = default)
    {
        var node = await Set.FindAsync([nodeId], cancellationToken);
        if (node == null)
            return false;

        // 防环：目标是自身或自身的后代时拒绝（含自环，源实现放行了自环）
        if (await IsSelfOrDescendantAsync(nodeId, newParentId, cancellationToken))
            return false;

        node.ParentId = newParentId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ==================== 内部 ====================

    /// <summary>candidateId 是否等于 ancestorId 或位于其子孙链上。</summary>
    private async Task<bool> IsSelfOrDescendantAsync(
        string ancestorId, string? candidateId, CancellationToken cancellationToken)
    {
        var currentId = candidateId;
        while (!string.IsNullOrEmpty(currentId))
        {
            if (currentId == ancestorId)
                return true;

            currentId = await Set
                .Where(c => c.Id == currentId)
                .Select(c => c.ParentId)
                .FirstOrDefaultAsync(cancellationToken);
        }
        return false;
    }

    /// <summary>
    /// 懒加载 DTO：不带 Children，HasChildren 按子表实时计算。
    /// 不填充 <see cref="TreeNodeDto{T}.Parent"/>：与批量路径 <see cref="ToLazyDtosAsync"/> 行为一致，
    /// 避免单节点写操作多 1 次 DB 查询；客户端需要父引用时用 ParentId 反查或调祖先路径接口。
    /// </summary>
    private async Task<TreeNodeDto<TNode>> ToLazyDtoAsync(TNode entity, CancellationToken ct)
    {
        var hasChildren = await Set.AnyAsync(c => c.ParentId == entity.Id, ct);

        return new TreeNodeDto<TNode>
        {
            Id = entity.Id,
            ParentId = entity.ParentId,
            Value = entity,
            Text = entity.Text(),
            SortOrder = entity.SortOrder,
            HasChildren = hasChildren,
            Children = null,
        };
    }

    private async Task<IReadOnlyList<TreeNodeDto<TNode>>> ToLazyDtosAsync(
        List<TNode> entities, CancellationToken ct)
    {
        if (entities.Count == 0)
            return [];

        // 批量计算 HasChildren，避免逐节点 AnyAsync
        var ids = entities.Select(e => e.Id).ToList();
        var parentIdsWithChildren = (await Set
                .Where(c => c.ParentId != null && ids.Contains(c.ParentId))
                .Select(c => c.ParentId!)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        return entities.Select(e => new TreeNodeDto<TNode>
        {
            Id = e.Id,
            ParentId = e.ParentId,
            Value = e,
            Text = e.Text(),
            SortOrder = e.SortOrder,
            HasChildren = parentIdsWithChildren.Contains(e.Id),
            Children = null,
        }).ToList();
    }
}

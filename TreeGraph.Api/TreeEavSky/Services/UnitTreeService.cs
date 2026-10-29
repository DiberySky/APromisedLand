using TreeGraph.Api.Data;
using TreeGraph.Api.GraphSyncSky;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Shared.Messaging;
using TreeGraph.Shared.NodeSky.Entities;
using TreeGraph.Shared.TreeEavSky;
using TreeGraph.Shared.TreeEavSky.Entities;
using TreeGraph.Shared.TreeEavSky.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TreeGraph.Api.TreeEavSky.Services;

/// <summary>
/// 写操作（创建/更新/删除/移动）成功后发布 InodeChangedEvent，
/// 供语义搜索向量同步与图同步消费（at-least-once，消费端幂等）。
/// </summary>
public class UnitTreeService(TreeGraphDbContext dbContext, IEventPublisher publishEndpoint) : ITreeService<UnitTree>
{
    /// <summary>显示文本：名称（来自 nodes）+ 可选简称。</summary>
    private static string BuildText(UnitTree c)
        => string.IsNullOrEmpty(c.Abbreviation) ? c.Name ?? string.Empty : $"{c.Name} 【{c.Abbreviation}】";

    public async Task<IReadOnlyList<TreeNodeDto<UnitTree>>> GetRootNodesAsync(string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        List<TreeNodeDto<UnitTree>> roots = [];

        if (rootId != null)
        {
            var rootItem = await dbContext.UnitTrees
                .Include(c => c.Node)
                .FirstAsync(c => c.Id == rootId, cancellationToken);

            roots.Add(rootItem.ToNodeDto(BuildText(rootItem)));
        }
        else
        {
            var items = await dbContext.UnitTrees
                .Include(c => c.Node)
                .Where(c => c.ParentId == null)
                .OrderBy(c => c.SortOrder)
                .ToListAsync(cancellationToken);
            roots = items.Select(c => c.ToNodeDto(BuildText(c))).ToList();
        }

        foreach (var rootNode in roots)
        {
            var children = await GetChildrenAsync(rootNode.Id, cancellationToken);

            rootNode.Children = children.ToList();
            rootNode.HasChildren = rootNode.Children.Count != 0;
            rootNode.Expanded = rootNode.Children.Count != 0;

            if (rootNode.Children == null) continue;

            foreach (var child in rootNode.Children)
            {
                child.Parent = rootNode.Value;
                child.Value!.Parent = rootNode.Value;
                var grandson = await GetChildrenAsync(child.Id, cancellationToken);
                child.Children = grandson.ToList();
                child.HasChildren = grandson.Count > 0;
            }
        }

        return roots;
    }

    public async Task<IReadOnlyList<TreeNodeDto<UnitTree>>> GetChildrenAsync(string parentId,
        CancellationToken cancellationToken = default)
    {
        var childrenItems = await dbContext.UnitTrees
            .Include(c => c.Node)
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);
        var children = childrenItems.Select(c => c.ToNodeDto(BuildText(c))).ToList();

        foreach (var child in children)
        {
            var parent = await dbContext.UnitTrees
                .Include(c => c.Node)
                .FirstAsync(c => c.Id == parentId, cancellationToken);

            child.Parent = parent;
            child.Value!.Parent = parent;

            var grandsonItems = await dbContext.UnitTrees
                .Include(c => c.Node)
                .Where(c => c.ParentId == child.Id)
                .OrderBy(c => c.SortOrder)
                .ToListAsync(cancellationToken);

            child.Children = grandsonItems.Select(c => c.ToNodeDto(BuildText(c))).ToList();
            child.HasChildren = grandsonItems.Count > 0;
        }

        return children;
    }

    /// <summary>
    /// 按 Id 读取单个节点（含父节点值，供宿主操作对话框展示父名/移动确认）。
    /// 节点不存在返回 null；Children 不填充（删除守卫用 HasChildren 标志，排序走懒加载）。
    /// </summary>
    public async Task<TreeNodeDto<UnitTree>?> GetNodeByIdAsync(string id,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.UnitTrees
            .Include(c => c.Node)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity == null) return null;

        var dto = entity.ToNodeDto(BuildText(entity));
        dto.HasChildren = await dbContext.UnitTrees
            .AnyAsync(c => c.ParentId == id, cancellationToken);

        if (!string.IsNullOrEmpty(entity.ParentId))
        {
            var parent = await dbContext.UnitTrees
                .Include(c => c.Node)
                .FirstOrDefaultAsync(c => c.Id == entity.ParentId, cancellationToken);
            dto.Parent = parent;
            if (dto.Value != null) dto.Value.Parent = parent;
        }

        return dto;
    }

    public async Task<IReadOnlyList<TreeNodeDto<UnitTree>>> QueryNodesAsync(TreeQueryParams queryParams,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.UnitTrees.Include(c => c.Node).AsQueryable();

        if (!string.IsNullOrEmpty(queryParams.ParentId))
            query = query.Where(c => c.ParentId == queryParams.ParentId);
        else if (queryParams.ParentId == null)
            query = query.Where(c => c.ParentId == null);

        if (!string.IsNullOrEmpty(queryParams.SearchTerm))
            query = query.Where(c => c.Node!.Name.Contains(queryParams.SearchTerm));

        if (queryParams.OnlyWithChildren)
            query = query.Where(c => c.HasChildren);

        var items = await query
            .OrderBy(c => c.SortOrder)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync(cancellationToken);
        return items.Select(c => c.ToNodeDto(BuildText(c))).ToList();
    }

    public async Task<TreeNodeDto<UnitTree>?> GetFullTreeAsync(string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 加载所有节点到内存
        var allNodes = await dbContext.UnitTrees
            .Include(c => c.Node)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);
        if (allNodes.Count == 0)
            return null;

        // 2. 确定起始根节点
        UnitTree? rootEntity;
        if (string.IsNullOrEmpty(rootId))
        {
            rootEntity = allNodes.FirstOrDefault(n => n.ParentId == null);
            if (rootEntity == null)
                return null; // 没有根节点，返回 null
        }
        else
        {
            rootEntity = allNodes.FirstOrDefault(n => n.Id == rootId);
            if (rootEntity == null)
                return null;
        }

        // 3. 建立 ID 字典，并组装父子关系（内存中填充 Children 集合）
        var dict = allNodes.ToDictionary(n => n.Id);
        foreach (var node in allNodes)
        {
            if (node.ParentId != null && dict.TryGetValue(node.ParentId, out var parent))
            {
            }
        }

        // 4. 递归构建 DTO 树
        TreeNodeDto<UnitTree> BuildDto(UnitTree entity)
        {
            var dto = entity.ToNodeDto(BuildText(entity));
            return dto;
        }

        var rootDto = BuildDto(rootEntity);
        return rootDto;
    }

    public async Task<TreeNodeDto<UnitTree>> CreateNodeAsync(TreeNodeDto<UnitTree> nodeDto,
        CancellationToken cancellationToken = default)
    {
        var id = string.IsNullOrEmpty(nodeDto.Id) ? Guid.NewGuid().ToString() : nodeDto.Id;
        var name = nodeDto.Text ?? string.Empty;

        var entity = new UnitTree
        {
            Id = id,
            ParentId = nodeDto.ParentId,
            SortOrder = nodeDto.SortOrder,
            CanHaveChildren = nodeDto.CanHaveChildren,
            HasChildren = false
        };

        if (!string.IsNullOrEmpty(entity.ParentId))
        {
            var parent = await dbContext.UnitTrees
                .Include(c => c.Node)
                .FirstOrDefaultAsync(c => c.Id == entity.ParentId, cancellationToken);
            // ★ 硬校验：父节点声明为叶子（CanHaveChildren=false）时拒绝创建子节点
            if (parent is not null && !parent.CanHaveChildren)
                throw new InvalidOperationException($"父节点「{parent.Name}」不允许包含子节点");
            if (parent != null && !parent.HasChildren)
            {
                parent.HasChildren = true;
                dbContext.Update(parent);
            }
        }

        // 名称唯一真源：nodes 注册表（同事务，先注册表后宿主）
        dbContext.Nodes.Add(new Node
        {
            Id = id,
            HostType = "UnitTree",
            Name = name,
        });
        await dbContext.UnitTrees.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 结构派生边 parent_of 落 PG：EdgeSky 独立事实源，树写路径直写，不经 Nebula 同步管道
        await TreeParentEdgeWriter.SyncAsync(dbContext, SeedSpaceIds.Unit, entity.Id, entity.ParentId, cancellationToken);

        await PublishAsync(entity.Id, InodeChangeKind.Saved, pathAffected: false, cancellationToken);

        return entity.ToNodeDto(name);
    }

    public async Task<TreeNodeDto<UnitTree>> UpdateNodeAsync(TreeNodeDto<UnitTree> nodeDto,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.UnitTrees
            .Include(c => c.Node)
            .FirstOrDefaultAsync(c => c.Id == nodeDto.Id, cancellationToken);
        if (entity == null)
            throw new KeyNotFoundException($"节点 {nodeDto.Id} 不存在");

        if (entity.Node != null)
            entity.Node.Name = nodeDto.Text ?? entity.Node.Name;
        entity.SortOrder = nodeDto.SortOrder;
        entity.CanHaveChildren = nodeDto.CanHaveChildren;

        // 不在此修改 ParentId，请使用 MoveNodeAsync
        entity.ParentId = nodeDto.ParentId;

        dbContext.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 结构派生边 parent_of 落 PG：EdgeSky 独立事实源，树写路径直写，不经 Nebula 同步管道
        await TreeParentEdgeWriter.SyncAsync(dbContext, SeedSpaceIds.Unit, entity.Id, entity.ParentId, cancellationToken);

        await PublishAsync(entity.Id, InodeChangeKind.Saved, pathAffected: true, cancellationToken);

        return entity.ToNodeDto(BuildText(entity));
    }

    public async Task<TreeNodeDto<UnitTree>> UpdateChildrenAsync(TreeNodeDto<UnitTree> nodeDto,
        CancellationToken cancellationToken = default)
    {
        var entityChildren =
            await dbContext.UnitTrees.Where(x => x.ParentId == nodeDto.Id).ToListAsync(cancellationToken);

        foreach (var entity in entityChildren)
        {
            entity.SortOrder = nodeDto.Children?.FirstOrDefault(x => x.Id == entity.Id)?.SortOrder ?? 0;
        }

        dbContext.UpdateRange(entityChildren);

        await dbContext.SaveChangesAsync(cancellationToken);

        return nodeDto;
    }

    public async Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        // 1. 查找节点
        var node = await dbContext.UnitTrees
            .FirstOrDefaultAsync(c => c.Id == nodeId, cancellationToken);

        if (node == null)
            return false;

        // 子项为空方可删除本项：仍有子节点时拒绝（不采用级联删除）
        var hasChildren = await dbContext.UnitTrees
            .AnyAsync(c => c.ParentId == nodeId, cancellationToken);
        if (hasChildren)
            throw new InvalidOperationException("该节点仍有子节点，无法删除。请先删除全部子节点。");

        // 引用为空方可删除本项：仍有边（edges）引用该节点时拒绝（不采用级联删除）；
        // 结构派生边 parent_of 随节点级联删除，不计入引用（否则任何子节点都无法删除）
        var hasEdges = await dbContext.Edges
            .AnyAsync(e => (e.SrcVid == nodeId || e.DstVid == nodeId)
                        && e.EdgeType != TreeGraph.Api.GraphSyncSky.GraphNaming.ParentOfEdge,
                cancellationToken);
        if (hasEdges)
            throw new InvalidOperationException("该节点仍被边引用，无法删除。请先删除引用该节点的边。");

        // 先清空自身数据才允许删本项：该节点仍有挂载实体数据时拒绝（不采用级联删除）
        var hasEntities = await dbContext.InodeEntities
            .AnyAsync(m => m.InodeId == nodeId, cancellationToken);
        if (hasEntities)
            throw new InvalidOperationException("该节点仍有挂载的实体数据，无法删除。请先清空其全部实体数据。");

        // 类型声明（inode_entitytypes）不含业务数据，随宿主一并清理，避免孤儿声明
        var declarations = await dbContext.InodeEntityTypes
            .Where(x => x.InodeId == nodeId)
            .ToListAsync(cancellationToken);
        dbContext.InodeEntityTypes.RemoveRange(declarations);

        dbContext.UnitTrees.Remove(node);

        // 同步注销 nodes 注册表行（先宿主后注册表）
        var nodeRow = await dbContext.Nodes.FindAsync([nodeId], cancellationToken);
        if (nodeRow != null) dbContext.Nodes.Remove(nodeRow);

        await dbContext.SaveChangesAsync(cancellationToken);

        await PublishAsync(nodeId, InodeChangeKind.Deleted, pathAffected: false, cancellationToken);

        return true;
    }

    public async Task<bool> MoveNodeAsync(string nodeId, string? newParentId,
        CancellationToken cancellationToken = default)
    {
        var node = await dbContext.UnitTrees.FindAsync(new object[] { nodeId }, cancellationToken);
        if (node == null)
            throw new KeyNotFoundException($"节点 '{nodeId}' 不存在");

        // 循环检测：禁止移动到自身子节点下
        if (await IsDescendantAsync(nodeId, newParentId, cancellationToken))
            return false;

        var oldParentId = node.ParentId;
        node.ParentId = newParentId;
        dbContext.Update(node);

        // 更新旧父节点 HasChildren
        if (!string.IsNullOrEmpty(oldParentId))
        {
            var oldParentChildrenCount = await dbContext.UnitTrees
                .Where(c => c.ParentId == oldParentId)
                .CountAsync(cancellationToken);
            if (oldParentChildrenCount == 0)
            {
                var oldParent =
                    await dbContext.UnitTrees.FindAsync(new object[] { oldParentId }, cancellationToken);
                if (oldParent != null)
                {
                    oldParent.HasChildren = false;
                    dbContext.Update(oldParent);
                }
            }
        }

        // 更新新父节点 HasChildren
        if (!string.IsNullOrEmpty(newParentId))
        {
            var newParent = await dbContext.UnitTrees
                .Include(c => c.Node)
                .FirstOrDefaultAsync(c => c.Id == newParentId, cancellationToken);
            // ★ 硬校验：目标父节点声明为叶子时拒绝移入
            if (newParent is not null && !newParent.CanHaveChildren)
                throw new InvalidOperationException($"目标父节点「{newParent.Name}」不允许包含子节点");
            if (newParent != null)
            {
                newParent.HasChildren = true;
                dbContext.Update(newParent);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // 结构派生边 parent_of 落 PG：EdgeSky 独立事实源，树写路径直写，不经 Nebula 同步管道
        await TreeParentEdgeWriter.SyncAsync(dbContext, SeedSpaceIds.Unit, nodeId, newParentId, cancellationToken);

        await PublishAsync(nodeId, InodeChangeKind.Saved, pathAffected: true, cancellationToken);

        return true;
    }

    /// <summary>发布 iNode 变更事件（向量同步 + 图同步；at-least-once，消费端幂等）。
    /// pathAffected：名称/层级位置变更（改名/移动）→ 消费端级联刷新全部后代的路径型文本。</summary>
    private async Task PublishAsync(string inodeId, InodeChangeKind kind, bool pathAffected, CancellationToken ct)
    {
        await publishEndpoint.Publish(new InodeChangedEvent
        {
            InodeId = inodeId,
            Kind = kind,
            PathAffected = pathAffected
        }, ct);
    }

    private async Task<bool> IsDescendantAsync(string ancestorId, string? nodeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(nodeId) || ancestorId == nodeId)
            return false;

        var sql = @"
            WITH RECURSIVE descendants AS (
                SELECT id FROM ""unit_trees"" WHERE id = @ancestorId
                UNION ALL
                SELECT c.id FROM ""unit_trees"" c
                INNER JOIN descendants d ON c.""parent_id"" = d.id
            )
            SELECT EXISTS (SELECT 1 FROM descendants WHERE id = @nodeId) AS ""Value""";

        var param1 = new NpgsqlParameter("@ancestorId", ancestorId);
        var param2 = new NpgsqlParameter("@nodeId", nodeId);
        var exists = await dbContext.Database
            .SqlQueryRaw<bool>(sql, param1, param2)
            .FirstOrDefaultAsync(cancellationToken);
        return exists;
    }

    public async Task<IReadOnlyList<string>> GetAncestorPathAsync(string nodeId,
        CancellationToken cancellationToken = default)
    {
        var path = new List<string>();
        var currentId = nodeId;

        while (!string.IsNullOrEmpty(currentId))
        {
            var node = await dbContext.UnitTrees
                .Where(c => c.Id == currentId)
                .Select(c => new { c.Id, c.ParentId })
                .FirstOrDefaultAsync(cancellationToken);

            if (node == null) break;

            path.Insert(0, node.Id);
            currentId = node.ParentId;
        }

        return path.AsReadOnly();
    }

    public async Task<IReadOnlyList<string>> GetAncestorPathAsyncAll(string nodeId,
        CancellationToken cancellationToken = default)
    {
        var path = new List<string>();
        var currentId = nodeId;

        while (!string.IsNullOrEmpty(currentId))
        {
            var node = await dbContext.UnitTrees
                .Where(c => c.Id == currentId)
                .Select(c => new { c.Id, c.ParentId })
                .FirstOrDefaultAsync(cancellationToken);

            if (node == null) break;

            path.Insert(0, node.Id);
            currentId = node.ParentId;
        }

        return path.AsReadOnly();
    }
}

using TreeGraph.Api.Data;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Shared.Messaging;
using TreeGraph.Shared.NodeSky.Entities;
using TreeGraph.Shared.TreeEavSky;
using TreeGraph.Shared.TreeEavSky.Entities;
using TreeGraph.Shared.TreeEavSky.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TreeGraph.Api.TreeEavSky.Services;

public class KnowledgeTreeService(TreeGraphDbContext dbContext, IEventPublisher publishEndpoint) : ITreeService<KnowledgeTree>
{
    private static string BuildText(KnowledgeTree c) => c.Name ?? string.Empty;

    public async Task<IReadOnlyList<TreeNodeDto<KnowledgeTree>>> GetRootNodesAsync(string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        List<TreeNodeDto<KnowledgeTree>> roots = [];

        if (rootId != null)
        {
            var rootItem = await dbContext.KnowledgeTrees
                .Include(c => c.Node)
                .FirstAsync(c => c.Id == rootId, cancellationToken);

            roots.Add(rootItem.ToNodeDto(BuildText(rootItem)));
        }
        else
        {
            var items = await dbContext.KnowledgeTrees
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

    public async Task<IReadOnlyList<TreeNodeDto<KnowledgeTree>>> GetChildrenAsync(string parentId,
        CancellationToken cancellationToken = default)
    {
        var childrenItems = await dbContext.KnowledgeTrees
            .Include(c => c.Node)
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);
        var children = childrenItems.Select(c => c.ToNodeDto(BuildText(c))).ToList();

        foreach (var child in children)
        {
            var parent = await dbContext.KnowledgeTrees
                .Include(c => c.Node)
                .FirstAsync(c => c.Id == parentId, cancellationToken);

            child.Parent = parent;
            child.Value!.Parent = parent;

            var grandsonItems = await dbContext.KnowledgeTrees
                .Include(c => c.Node)
                .Where(c => c.ParentId == child.Id)
                .OrderBy(c => c.SortOrder)
                .ToListAsync(cancellationToken);

            child.Children = grandsonItems.Select(c => c.ToNodeDto(BuildText(c))).ToList();
            child.HasChildren = grandsonItems.Count > 0;
        }

        return children;
    }

    public async Task<IReadOnlyList<TreeNodeDto<KnowledgeTree>>> QueryNodesAsync(TreeQueryParams queryParams,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.KnowledgeTrees.Include(c => c.Node).AsQueryable();

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

    public async Task<TreeNodeDto<KnowledgeTree>?> GetFullTreeAsync(string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        var allNodes = await dbContext.KnowledgeTrees
            .Include(c => c.Node)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);
        if (allNodes.Count == 0)
            return null;

        KnowledgeTree? rootEntity;
        if (string.IsNullOrEmpty(rootId))
        {
            rootEntity = allNodes.FirstOrDefault(n => n.ParentId == null);
            if (rootEntity == null)
                return null;
        }
        else
        {
            rootEntity = allNodes.FirstOrDefault(n => n.Id == rootId);
            if (rootEntity == null)
                return null;
        }

        var dict = allNodes.ToDictionary(n => n.Id);
        foreach (var node in allNodes)
        {
            if (node.ParentId != null && dict.TryGetValue(node.ParentId, out var parent))
            {
            }
        }

        TreeNodeDto<KnowledgeTree> BuildDto(KnowledgeTree entity)
        {
            var dto = entity.ToNodeDto(BuildText(entity));
            return dto;
        }

        var rootDto = BuildDto(rootEntity);
        return rootDto;
    }

    public async Task<TreeNodeDto<KnowledgeTree>> CreateNodeAsync(TreeNodeDto<KnowledgeTree> nodeDto,
        CancellationToken cancellationToken = default)
    {
        var id = string.IsNullOrEmpty(nodeDto.Id) ? Guid.NewGuid().ToString() : nodeDto.Id;
        var name = nodeDto.Text ?? string.Empty;

        var entity = new KnowledgeTree
        {
            Id = id,
            ParentId = nodeDto.ParentId,
            SortOrder = nodeDto.SortOrder,
            CanHaveChildren = nodeDto.CanHaveChildren,
            HasChildren = false
        };

        if (!string.IsNullOrEmpty(entity.ParentId))
        {
            var parent = await dbContext.KnowledgeTrees
                .Include(c => c.Node)
                .FirstOrDefaultAsync(c => c.Id == entity.ParentId, cancellationToken);
            if (parent is not null && !parent.CanHaveChildren)
                throw new InvalidOperationException($"父节点「{parent.Name}」不允许包含子节点");
            if (parent != null && !parent.HasChildren)
            {
                parent.HasChildren = true;
                dbContext.Update(parent);
            }
        }

        dbContext.Nodes.Add(new Node
        {
            Id = id,
            HostType = "KnowledgeTree",
            Name = name,
        });
        await dbContext.KnowledgeTrees.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await PublishAsync(entity.Id, InodeChangeKind.Saved, pathAffected: false, cancellationToken);

        return entity.ToNodeDto(name);
    }

    public async Task<TreeNodeDto<KnowledgeTree>> UpdateNodeAsync(TreeNodeDto<KnowledgeTree> nodeDto,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.KnowledgeTrees
            .Include(c => c.Node)
            .FirstOrDefaultAsync(c => c.Id == nodeDto.Id, cancellationToken);
        if (entity == null)
            throw new KeyNotFoundException($"节点 {nodeDto.Id} 不存在");

        if (entity.Node != null)
            entity.Node.Name = nodeDto.Text ?? entity.Node.Name;
        entity.SortOrder = nodeDto.SortOrder;
        entity.CanHaveChildren = nodeDto.CanHaveChildren;

        entity.ParentId = nodeDto.ParentId;

        dbContext.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        await PublishAsync(entity.Id, InodeChangeKind.Saved, pathAffected: true, cancellationToken);

        return entity.ToNodeDto(BuildText(entity));
    }

    public async Task<TreeNodeDto<KnowledgeTree>> UpdateChildrenAsync(TreeNodeDto<KnowledgeTree> nodeDto, CancellationToken cancellationToken = default)
    {
        var entityChildren = await dbContext.KnowledgeTrees.Where(x => x.ParentId == nodeDto.Id).ToListAsync(cancellationToken);

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
        var node = await dbContext.KnowledgeTrees
            .FirstOrDefaultAsync(c => c.Id == nodeId, cancellationToken);

        if (node == null)
            return false;

        var hasChildren = await dbContext.KnowledgeTrees
            .AnyAsync(c => c.ParentId == nodeId, cancellationToken);
        if (hasChildren)
            throw new InvalidOperationException("该节点仍有子节点，无法删除。请先删除全部子节点。");

        // 引用为空方可删除本项：仍有关系引用该节点时拒绝（不采用级联删除）
        var hasRelations = await dbContext.RelationAttributeValues
            .AnyAsync(r => r.EntityId == nodeId || r.ToNodeId == nodeId, cancellationToken);
        if (hasRelations)
            throw new InvalidOperationException("该节点仍被关系引用，无法删除。请先删除引用该节点的关系。");

        var hasEntities = await dbContext.InodeEntities
            .AnyAsync(m => m.InodeId == nodeId, cancellationToken);
        if (hasEntities)
            throw new InvalidOperationException("该节点仍有挂载的实体数据，无法删除。请先清空其全部实体数据。");

        var declarations = await dbContext.InodeEntityTypes
            .Where(x => x.InodeId == nodeId)
            .ToListAsync(cancellationToken);
        dbContext.InodeEntityTypes.RemoveRange(declarations);

        dbContext.KnowledgeTrees.Remove(node);

        var nodeRow = await dbContext.Nodes.FindAsync([nodeId], cancellationToken);
        if (nodeRow != null) dbContext.Nodes.Remove(nodeRow);

        await dbContext.SaveChangesAsync(cancellationToken);

        await PublishAsync(nodeId, InodeChangeKind.Deleted, pathAffected: false, cancellationToken);

        return true;
    }

    public async Task<bool> MoveNodeAsync(string nodeId, string? newParentId,
        CancellationToken cancellationToken = default)
    {
        var node = await dbContext.KnowledgeTrees.FindAsync(new object[] { nodeId }, cancellationToken);
        if (node == null)
            throw new KeyNotFoundException($"节点 '{nodeId}' 不存在");

        if (await IsDescendantAsync(nodeId, newParentId, cancellationToken))
            return false;

        var oldParentId = node.ParentId;
        node.ParentId = newParentId;
        dbContext.Update(node);

        if (!string.IsNullOrEmpty(oldParentId))
        {
            var oldParentChildrenCount = await dbContext.KnowledgeTrees
                .Where(c => c.ParentId == oldParentId)
                .CountAsync(cancellationToken);
            if (oldParentChildrenCount == 0)
            {
                var oldParent =
                    await dbContext.KnowledgeTrees.FindAsync(new object[] { oldParentId }, cancellationToken);
                if (oldParent != null)
                {
                    oldParent.HasChildren = false;
                    dbContext.Update(oldParent);
                }
            }
        }

        if (!string.IsNullOrEmpty(newParentId))
        {
            var newParent = await dbContext.KnowledgeTrees
                .Include(c => c.Node)
                .FirstOrDefaultAsync(c => c.Id == newParentId, cancellationToken);
            if (newParent is not null && !newParent.CanHaveChildren)
                throw new InvalidOperationException($"目标父节点「{newParent.Name}」不允许包含子节点");
            if (newParent != null)
            {
                newParent.HasChildren = true;
                dbContext.Update(newParent);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await PublishAsync(nodeId, InodeChangeKind.Saved, pathAffected: true, cancellationToken);

        return true;
    }

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
                SELECT id FROM ""knowledge_trees"" WHERE id = @ancestorId
                UNION ALL
                SELECT c.id FROM ""knowledge_trees"" c
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

    public async Task<IReadOnlyList<string>> GetAncestorPathAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var path = new List<string>();
        var currentId = nodeId;

        while (!string.IsNullOrEmpty(currentId))
        {
            var node = await dbContext.KnowledgeTrees
                .Where(c => c.Id == currentId)
                .Select(c => new { c.Id, c.ParentId })
                .FirstOrDefaultAsync(cancellationToken);

            if (node == null) break;

            path.Insert(0, node.Id);
            currentId = node.ParentId;
        }

        return path.AsReadOnly();
    }

    public async Task<IReadOnlyList<string>> GetAncestorPathAsyncAll(string nodeId, CancellationToken cancellationToken = default)
    {
        var path = new List<string>();
        var currentId = nodeId;

        while (!string.IsNullOrEmpty(currentId))
        {
            var node = await dbContext.KnowledgeTrees
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

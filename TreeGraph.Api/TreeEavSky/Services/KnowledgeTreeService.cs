using TreeGraph.Api.Data;
using TreeGraph.Shared.TreeEavSky;
using TreeGraph.Shared.TreeEavSky.Entities;
using TreeGraph.Shared.TreeEavSky.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TreeGraph.Api.TreeEavSky.Services;

public class KnowledgeTreeService(TreeGraphDbContext dbContext) : ITreeService<KnowledgeTree>
{
    public async Task<IReadOnlyList<TreeNodeDto<KnowledgeTree>>> GetRootNodesAsync(string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        List<TreeNodeDto<KnowledgeTree>> roots = [];

        if (rootId != null)
        {
            var rootItem = await dbContext.KnowledgeTrees
                .FirstAsync(c => c.Id == rootId, cancellationToken);

            roots.Add(rootItem.ToNodeDto());
        }
        else
        {
            roots = await dbContext.KnowledgeTrees
                .Where(c => c.ParentId == null)
                .OrderBy(c => c.SortOrder)
                .Select(c => c.ToNodeDto())
                .ToListAsync(cancellationToken);
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
        var children = await dbContext.KnowledgeTrees
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.SortOrder)
            .Select(c => c.ToNodeDto())
            .ToListAsync(cancellationToken);

        foreach (var child in children)
        {
            var parent = await dbContext.KnowledgeTrees
                .FirstAsync(c => c.Id == parentId, cancellationToken);

            child.Parent = parent;
            child.Value!.Parent = parent;

            var grandson = await dbContext.KnowledgeTrees
                .Where(c => c.ParentId == child.Id)
                .OrderBy(c => c.SortOrder)
                .Select(c => c.ToNodeDto())
                .ToListAsync(cancellationToken);

            child.Children = grandson.ToList();
            child.HasChildren = grandson.Count > 0;
        }

        return children;
    }

    public async Task<IReadOnlyList<TreeNodeDto<KnowledgeTree>>> QueryNodesAsync(TreeQueryParams queryParams,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.KnowledgeTrees.AsQueryable();

        if (!string.IsNullOrEmpty(queryParams.ParentId))
            query = query.Where(c => c.ParentId == queryParams.ParentId);
        else if (queryParams.ParentId == null)
            query = query.Where(c => c.ParentId == null);

        if (!string.IsNullOrEmpty(queryParams.SearchTerm))
            query = query.Where(c => c.Name.Contains(queryParams.SearchTerm));

        if (queryParams.OnlyWithChildren)
            query = query.Where(c => c.HasChildren);

        var result = await query
            .OrderBy(c => c.SortOrder)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .Select(c => c.ToNodeDto())
            .ToListAsync(cancellationToken);
        return result;
    }

    public async Task<TreeNodeDto<KnowledgeTree>?> GetFullTreeAsync(string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 加载所有节点到内存
        var allNodes = await dbContext.KnowledgeTrees
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);
        if (allNodes.Count == 0)
            return null;

        // 2. 确定起始根节点
        KnowledgeTree? rootEntity;
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
                // parent.Children ??= new List<KnowledgeTree>();
                // parent.Children.Add(node);
            }
        }

        // 4. 递归构建 DTO 树，并对子节点排序
        TreeNodeDto<KnowledgeTree> BuildDto(KnowledgeTree entity)
        {
            var dto = entity.ToNodeDto();
            // if (entity.Children != null && entity.Children.Any())
            // {
            //     dto.Children = entity.Children
            //         .OrderBy(c => c.SortOrder)
            //         .Select(child => BuildDto(child))
            //         .ToList();
            //     dto.HasChildren = true;
            // }
            // else
            // {
            //     dto.HasChildren = false;
            // }

            return dto;
        }

        var rootDto = BuildDto(rootEntity);
        return rootDto;
    }

    public async Task<TreeNodeDto<KnowledgeTree>> CreateNodeAsync(TreeNodeDto<KnowledgeTree> nodeDto,
        CancellationToken cancellationToken = default)
    {
        var entity = new KnowledgeTree
        {
            Id = nodeDto.Id ?? Guid.NewGuid().ToString(),
            Name = nodeDto.Text ?? string.Empty,
            ParentId = nodeDto.ParentId,
            SortOrder = nodeDto.SortOrder,
            CanHaveChildren = nodeDto.CanHaveChildren,
            HasChildren = false
        };

        if (!string.IsNullOrEmpty(entity.ParentId))
        {
            var parent = await dbContext.KnowledgeTrees.FindAsync(new object[] { entity.ParentId }, cancellationToken);
            // ★ 硬校验：父节点声明为叶子（CanHaveChildren=false）时拒绝创建子节点
            if (parent is not null && !parent.CanHaveChildren)
                throw new InvalidOperationException($"父节点「{parent.Name}」不允许包含子节点");
            if (parent != null && !parent.HasChildren)
            {
                parent.HasChildren = true;
                dbContext.Update(parent);
            }
        }

        await dbContext.KnowledgeTrees.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToNodeDto();
    }

    public async Task<TreeNodeDto<KnowledgeTree>> UpdateNodeAsync(TreeNodeDto<KnowledgeTree> nodeDto,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.KnowledgeTrees.FindAsync(new object[] { nodeDto.Id }, cancellationToken);
        if (entity == null)
            throw new KeyNotFoundException($"节点 {nodeDto.Id} 不存在");

        entity.Name = nodeDto.Text ?? entity.Name;
        entity.SortOrder = nodeDto.SortOrder;
        entity.CanHaveChildren = nodeDto.CanHaveChildren;

        // 不在此修改 ParentId，请使用 MoveNodeAsync
        entity.ParentId = nodeDto.ParentId;

        dbContext.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToNodeDto();
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
        // 1. 查找节点
        var node = await dbContext.KnowledgeTrees
            .FirstOrDefaultAsync(c => c.Id == nodeId, cancellationToken);

        if (node == null)
            return false;

        // 2. 子项为空方可删除本项：仍有子节点时拒绝（不采用级联删除）
        var hasChildren = await dbContext.KnowledgeTrees
            .AnyAsync(c => c.ParentId == nodeId, cancellationToken);
        if (hasChildren)
            throw new InvalidOperationException("该节点仍有子节点，无法删除。请先删除全部子节点。");

        // 引用为空方可删除本项：仍有边引用该节点时拒绝（不采用级联删除）
        var hasEdges = await dbContext.EdgeInstances
            .AnyAsync(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId, cancellationToken);
        if (hasEdges)
            throw new InvalidOperationException("该节点仍被边引用，无法删除。请先删除引用该节点的边。");

        // 先清空自身数据才允许删本项：该节点仍有挂载实体数据时拒绝（不采用级联删除）
        var hasEntities = await dbContext.InodeEntities
            .AnyAsync(m => m.InodeId == nodeId, cancellationToken);
        if (hasEntities)
            throw new InvalidOperationException("该节点仍有挂载的实体数据，无法删除。请先清空其全部实体数据。");

        // 3. 删除节点
        dbContext.KnowledgeTrees.Remove(node);

        await dbContext.SaveChangesAsync(cancellationToken);

        // 5. 更新父节点的 HasChildren
        // if (!string.IsNullOrEmpty(parentId))
        // {
        //     var parentStillHasChildren = await dbContext.KnowledgeTrees
        //         .AnyAsync(c => c.ParentId == parentId, cancellationToken);
        //
        //     if (!parentStillHasChildren)
        //     {
        //         var parent = await dbContext.KnowledgeTrees
        //             .FindAsync(new object[] { parentId }, cancellationToken);
        //         if (parent != null)
        //         {
        //             parent.HasChildren = false;
        //             await dbContext.SaveChangesAsync(cancellationToken);
        //         }
        //     }
        // }

        return true;
    }

    public async Task<bool> MoveNodeAsync(string nodeId, string? newParentId,
        CancellationToken cancellationToken = default)
    {
        var node = await dbContext.KnowledgeTrees.FindAsync(new object[] { nodeId }, cancellationToken);
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

        // 更新新父节点 HasChildren
        if (!string.IsNullOrEmpty(newParentId))
        {
            var newParent = await dbContext.KnowledgeTrees.FindAsync(new object[] { newParentId }, cancellationToken);
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
        return true;
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

    /// <summary>
    /// 优化版：减少内存占用（逐层查询）
    /// 如果树很深，不需要加载全部节点：
    ///
    /// 方案	   查询次数	        内存占用	适用场景
    /// 逐层查询	N 次（路径深度）	1 个节点	节点数很大、树很深
    /// </summary>
    /// <param name="nodeId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<string>> GetAncestorPathAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var path = new List<string>();
        var currentId = nodeId;

        while (!string.IsNullOrEmpty(currentId))
        {
            // 只查当前节点
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

    /// <summary>
    /// 方案	        查询次数	内存占用	适用场景
    /// 一次加载全部	1 次	全部节点	节点数 小于 一万
    /// </summary>
    /// <param name="nodeId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<string>> GetAncestorPathAsyncAll(string nodeId, CancellationToken cancellationToken = default)
    {
        var path = new List<string>();
        var currentId = nodeId;

        while (!string.IsNullOrEmpty(currentId))
        {
            // 只查当前节点
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

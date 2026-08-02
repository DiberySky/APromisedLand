// APromisedLand.Api.Projects.DiberyTree.Services/CategoryTreeService.cs

using Microsoft.EntityFrameworkCore;
using Npgsql;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Api.Data;
using APromisedLand.Api.Projects.DiberyTree.Interface;
using APromisedLand.Shared.DiberyTree;

namespace APromisedLand.Api.Projects.DiberyTree.Services;

public class CategoryTreeService(DiberyDbContext dbContext) : ITreeService<CategoryTree>
{
    public async Task<IReadOnlyList<TreeNodeDto<CategoryTree>>> GetRootNodesAsync(string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        List<TreeNodeDto<CategoryTree>> roots = [];

        if (rootId != null)
        {
            var rootItem = await dbContext.CategoryTrees
                .FirstAsync(c => c.Id == rootId, cancellationToken);

            roots.Add(rootItem.ToNodeDto());
        }
        else
        {
            roots = await dbContext.CategoryTrees
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

    public async Task<IReadOnlyList<TreeNodeDto<CategoryTree>>> GetChildrenAsync(string parentId,
        CancellationToken cancellationToken = default)
    {
        var children = await dbContext.CategoryTrees
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.SortOrder)
            .Select(c => c.ToNodeDto())
            .ToListAsync(cancellationToken);

        foreach (var child in children)
        {
            var parent = await dbContext.CategoryTrees
                .FirstAsync(c => c.Id == parentId, cancellationToken);

            child.Parent = parent;
            child.Value!.Parent = parent;

            var grandson = await dbContext.CategoryTrees
                .Where(c => c.ParentId == child.Id)
                .OrderBy(c => c.SortOrder)
                .Select(c => c.ToNodeDto())
                .ToListAsync(cancellationToken);

            child.Children = grandson.ToList();
            child.HasChildren = grandson.Count > 0;
        }

        return children;
    }

    public async Task<IReadOnlyList<TreeNodeDto<CategoryTree>>> QueryNodesAsync(TreeQueryParams queryParams,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.CategoryTrees.AsQueryable();

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

    public async Task<TreeNodeDto<CategoryTree>?> GetFullTreeAsync(string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 加载所有节点到内存
        var allNodes = await dbContext.CategoryTrees
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);
        if (allNodes.Count == 0)
            return null;

        // 2. 确定起始根节点
        CategoryTree? rootEntity;
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
                // parent.Children ??= new List<CategoryTree>();
                // parent.Children.Add(node);
            }
        }

        // 4. 递归构建 DTO 树，并对子节点排序
        TreeNodeDto<CategoryTree> BuildDto(CategoryTree entity)
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

    public async Task<TreeNodeDto<CategoryTree>> CreateNodeAsync(TreeNodeDto<CategoryTree> nodeDto,
        CancellationToken cancellationToken = default)
    {
        var entity = new CategoryTree
        {
            Id = nodeDto.Id ?? Guid.NewGuid().ToString(),
            Name = nodeDto.Text ?? string.Empty,
            ParentId = nodeDto.ParentId,
            SortOrder = nodeDto.SortOrder,
            HasChildren = false
        };

        if (!string.IsNullOrEmpty(entity.ParentId))
        {
            var parent = await dbContext.CategoryTrees.FindAsync(new object[] { entity.ParentId }, cancellationToken);
            if (parent != null && !parent.HasChildren)
            {
                parent.HasChildren = true;
                dbContext.Update(parent);
            }
        }

        await dbContext.CategoryTrees.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToNodeDto();
    }

    public async Task<TreeNodeDto<CategoryTree>> UpdateNodeAsync(TreeNodeDto<CategoryTree> nodeDto,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CategoryTrees.FindAsync(new object[] { nodeDto.Id }, cancellationToken);
        if (entity == null)
            throw new KeyNotFoundException($"节点 {nodeDto.Id} 不存在");

        entity.Name = nodeDto.Text ?? entity.Name;
        entity.SortOrder = nodeDto.SortOrder;
        
        // 不在此修改 ParentId，请使用 MoveNodeAsync
        entity.ParentId = nodeDto.ParentId;
        
        dbContext.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToNodeDto();
    }

    public async Task<TreeNodeDto<CategoryTree>> UpdateChildrenAsync(TreeNodeDto<CategoryTree> nodeDto, CancellationToken cancellationToken = default)
    {
        var entityChildren = await dbContext.CategoryTrees.Where(x => x.ParentId == nodeDto.Id).ToListAsync(cancellationToken);

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
        var node = await dbContext.CategoryTrees
            .FirstOrDefaultAsync(c => c.Id == nodeId, cancellationToken);

        if (node == null)
            return false;

        // 2. 检查是否为叶子节点（没有子节点）
        // var hasChildren = await dbContext.CategoryTrees
        //     .AnyAsync(c => c.ParentId == nodeId, cancellationToken);
        //
        // if (hasChildren)
        //     throw new InvalidOperationException("只能删除叶子节点，请先删除子节点");

        // 3. 获取父节点ID
        // var parentId = node.ParentId;

        // 4. 删除节点
        dbContext.CategoryTrees.Remove(node);
        
        await dbContext.SaveChangesAsync(cancellationToken);

        // 5. 更新父节点的 HasChildren
        // if (!string.IsNullOrEmpty(parentId))
        // {
        //     var parentStillHasChildren = await dbContext.CategoryTrees
        //         .AnyAsync(c => c.ParentId == parentId, cancellationToken);
        //
        //     if (!parentStillHasChildren)
        //     {
        //         var parent = await dbContext.CategoryTrees
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
        var node = await dbContext.CategoryTrees.FindAsync(new object[] { nodeId }, cancellationToken);
        if (node == null)
            return false;

        // 循环检测
        if (await IsDescendantAsync(nodeId, newParentId, cancellationToken))
            return false;

        var oldParentId = node.ParentId;
        node.ParentId = newParentId;
        dbContext.Update(node);

        // 更新旧父节点 HasChildren
        if (!string.IsNullOrEmpty(oldParentId))
        {
            var oldParentChildrenCount = await dbContext.CategoryTrees
                .Where(c => c.ParentId == oldParentId)
                .CountAsync(cancellationToken);
            if (oldParentChildrenCount == 0)
            {
                var oldParent =
                    await dbContext.CategoryTrees.FindAsync(new object[] { oldParentId }, cancellationToken);
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
            var newParent = await dbContext.CategoryTrees.FindAsync(new object[] { newParentId }, cancellationToken);
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
                SELECT id FROM ""CategoryTrees"" WHERE id = @ancestorId
                UNION ALL
                SELECT c.id FROM ""CategoryTrees"" c
                INNER JOIN descendants d ON c.""ParentId"" = d.id
            )
            SELECT EXISTS (SELECT 1 FROM descendants WHERE id = @nodeId);";

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
            var node = await dbContext.CategoryTrees
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
            var node = await dbContext.CategoryTrees
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

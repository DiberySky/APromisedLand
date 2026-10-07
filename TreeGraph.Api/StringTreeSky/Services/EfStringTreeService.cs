using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Api.StringTreeSky.Entities;
using TreeGraph.Api.StringTreeSky.Mapping;
using TreeGraph.Shared.StringTreeSky.Contracts;

namespace TreeGraph.Api.StringTreeSky.Services;

public class EfStringTreeService : IStringTreeService
{
    private readonly TreeGraphDbContext _db;
    private readonly DbContextOptions<TreeGraphDbContext> _options;

    public EfStringTreeService(TreeGraphDbContext db, DbContextOptions<TreeGraphDbContext> options)
    {
        _db = db;
        _options = options;
    }

    public async Task<List<StringNodeDto>> GetRootNodesAsync(CancellationToken ct = default)
    {
        var rows = await _db.StringTreeSkyNodes
            .Where(n => n.ParentId == null)
            .OrderBy(n => n.SortOrder).ThenBy(n => n.Id)
            .Select(n => new
            {
                Node = n,
                HasChildren = _db.StringTreeSkyNodes.Any(c => c.ParentId == n.Id)
            })
            .ToListAsync(ct);

        return rows.Select(r => r.Node.ToDto(r.HasChildren)).ToList();
    }

    public async Task<List<StringNodeDto>> GetChildrenAsync(string parentId, CancellationToken ct = default)
    {
        var rows = await _db.StringTreeSkyNodes
            .Where(n => n.ParentId == parentId)
            .OrderBy(n => n.SortOrder).ThenBy(n => n.Id)
            .Select(n => new
            {
                Node = n,
                HasChildren = _db.StringTreeSkyNodes.Any(c => c.ParentId == n.Id)
            })
            .ToListAsync(ct);

        return rows.Select(r => r.Node.ToDto(r.HasChildren)).ToList();
    }

    public async Task<StringNodeDto?> GetNodeAsync(string id, CancellationToken ct = default)
    {
        var entity = await _db.StringTreeSkyNodes.FindAsync(new object?[] { id }, ct);
        if (entity == null) return null;
        var hasChildren = await _db.StringTreeSkyNodes.AnyAsync(c => c.ParentId == id, ct);
        return entity.ToDto(hasChildren);
    }

    public async Task<List<StringNodeDto>> GetAncestorPathAsync(string id, CancellationToken ct = default)
    {
        var path = new List<StringNodeDto>();
        var current = await _db.StringTreeSkyNodes.FindAsync(new object?[] { id }, ct);
        while (current != null)
        {
            var hasChildren = await _db.StringTreeSkyNodes.AnyAsync(c => c.ParentId == current.Id, ct);
            path.Insert(0, current.ToDto(hasChildren));
            if (current.ParentId is null) break;
            current = await _db.StringTreeSkyNodes.FindAsync(new object?[] { current.ParentId }, ct);
        }
        return path;
    }

    public async Task<StringNodeDto> CreateNodeAsync(StringNodeDto dto, CancellationToken ct = default)
    {
        // 决定 EntityType：
        //   1. 显式传入（非 "auto"）→ 用之
        //   2. 有父节点 → 继承父节点
        //   3. 无父节点（根/空间）且未指定 → 默认共享（旧行为）；
        //      独立类型由 SpaceController 显式指定
        string entityType;
        if (!string.IsNullOrWhiteSpace(dto.EntityType)
            && dto.EntityType != "auto")
        {
            entityType = dto.EntityType;
        }
        else if (dto.ParentId is not null)
        {
            var parent = await _db.StringTreeSkyNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == dto.ParentId, ct);
            entityType = parent?.EntityType ?? StringTreeEntityTypes.Node;
        }
        else
        {
            entityType = StringTreeEntityTypes.Node;
        }

        var entity = new StringNodeEntity
        {
            Id = string.IsNullOrWhiteSpace(dto.Id)
                ? Guid.NewGuid().ToString("D")
                : dto.Id,
            Name = dto.Name?.Trim() ?? "未命名",
            ParentId = dto.ParentId,
            Description = dto.Description,
            SortOrder = await NextSortOrderAsync(dto.ParentId, ct),
            EntityType = entityType
        };
        _db.StringTreeSkyNodes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.ToDto(false);
    }

    public async Task<StringNodeDto?> UpdateNodeAsync(string id, StringNodeDto dto, CancellationToken ct = default)
    {
        var entity = await _db.StringTreeSkyNodes.FindAsync(new object?[] { id }, ct);
        if (entity == null) return null;

        entity.Name = dto.Name?.Trim() ?? entity.Name;
        entity.Description = dto.Description;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        var hasChildren = await _db.StringTreeSkyNodes.AnyAsync(c => c.ParentId == id, ct);
        return entity.ToDto(hasChildren);
    }

    public async Task<bool> DeleteNodeAsync(string id, CancellationToken ct = default)
    {
        var entity = await _db.StringTreeSkyNodes.FindAsync(new object?[] { id }, ct);
        if (entity == null) return false;

        // 1) 收集全部子孙 Id（含自身），用于级联清理 EAV 数据
        var nodeIds = new List<string> { id };
        await CollectDescendantIdsAsync(id, nodeIds, ct);

        // 2) EAV 清理 + 树删除在同一事务内原子提交
        //    （StringTreeSky 已并入 TreeGraphDbContext；节点 Id 即 EAV EntityId，
        //     无需任何 Id 映射。Npgsql 重试执行策略要求事务整体作为可重试单元。）
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // 每次重试使用全新上下文，避免前次失败留下的被跟踪实体污染重试
            await using var dbx = new TreeGraphDbContext(_options);
            await using var tx = await dbx.Database.BeginTransactionAsync(ct);

            // ★ 按 (EntityType, EntityId) 分组清理：独立空间子树的
            //    EntityType 为 "StringTreeNode:{spaceId}"，不能再按固定常量清理
            var nodes = await dbx.StringTreeSkyNodes
                .Where(n => nodeIds.Contains(n.Id))
                .Select(n => new { n.Id, n.EntityType })
                .ToListAsync(ct);

            foreach (var g in nodes.GroupBy(n => n.EntityType))
            {
                var ids = g.Select(x => x.Id).ToList();

                await dbx.AttributeValues
                    .Where(v => v.EntityType == g.Key && ids.Contains(v.EntityId))
                    .ExecuteDeleteAsync(ct);

                await dbx.CustomTableRows
                    .Where(r => r.ParentEntityType == g.Key && ids.Contains(r.ParentEntityId))
                    .ExecuteDeleteAsync(ct);
            }

            // 3) 物理删除节点（递归）
            await DeleteRecursiveAsync(dbx, id, ct);
            await dbx.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
        });
        return true;
    }

    private static async Task DeleteRecursiveAsync(TreeGraphDbContext db, string id, CancellationToken ct)
    {
        var children = await db.StringTreeSkyNodes.Where(n => n.ParentId == id).ToListAsync(ct);
        foreach (var child in children)
        {
            await DeleteRecursiveAsync(db, child.Id, ct);
        }
        var entity = await db.StringTreeSkyNodes.FindAsync(new object?[] { id }, ct);
        if (entity != null) db.StringTreeSkyNodes.Remove(entity);
    }

    private async Task CollectDescendantIdsAsync(string parentId, List<string> sink, CancellationToken ct)
    {
        var childIds = await _db.StringTreeSkyNodes
            .Where(n => n.ParentId == parentId)
            .Select(n => n.Id)
            .ToListAsync(ct);

        foreach (var childId in childIds)
        {
            sink.Add(childId);
            await CollectDescendantIdsAsync(childId, sink, ct);
        }
    }

    public async Task<bool> MoveNodeAsync(string id, string? newParentId, int newSortOrder, CancellationToken ct = default)
    {
        if (id == newParentId) return false;

        var entity = await _db.StringTreeSkyNodes.FindAsync(new object?[] { id }, ct);
        if (entity == null) return false;

        if (newParentId is not null && await WouldCreateCycleAsync(id, newParentId, ct))
            return false;

        entity.ParentId = newParentId;
        entity.SortOrder = newSortOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> WouldCreateCycleAsync(string nodeId, string newParentId, CancellationToken ct)
    {
        var currentId = newParentId;
        var guard = 0;
        while (currentId is not null && guard++ < 1000)
        {
            if (currentId == nodeId) return true;
            var parent = await _db.StringTreeSkyNodes.FindAsync(new object?[] { currentId }, ct);
            if (parent == null) break;
            currentId = parent.ParentId;
        }
        return false;
    }

    public async Task<bool> SortChildrenAsync(string parentId, IReadOnlyList<string> orderedIds, CancellationToken ct = default)
    {
        var children = await _db.StringTreeSkyNodes.Where(n => n.ParentId == parentId).ToListAsync(ct);
        var map = children.ToDictionary(c => c.Id);
        for (int i = 0; i < orderedIds.Count; i++)
        {
            if (map.TryGetValue(orderedIds[i], out var child))
            {
                child.SortOrder = i;
                child.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<int> NextSortOrderAsync(string? parentId, CancellationToken ct)
    {
        var max = await _db.StringTreeSkyNodes
            .Where(n => n.ParentId == parentId)
            .Select(n => (int?)n.SortOrder)
            .MaxAsync(ct);
        return (max ?? -1) + 1;
    }
}

using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.StringTreeSky.Data;
using TreeGraph.Api.StringTreeSky.Entities;
using TreeGraph.Api.StringTreeSky.Mapping;
using TreeGraph.StringTree.Contracts;

namespace TreeGraph.Api.StringTreeSky.Services;

public class EfStringTreeService : IStringTreeService
{
    private readonly StringTreeDbContext _db;

    public EfStringTreeService(StringTreeDbContext db) => _db = db;

    public async Task<List<StringNodeDto>> GetRootNodesAsync(CancellationToken ct = default)
    {
        var rows = await _db.StringNodes
            .Where(n => n.ParentId == null)
            .OrderBy(n => n.SortOrder).ThenBy(n => n.Id)
            .Select(n => new
            {
                Node = n,
                HasChildren = _db.StringNodes.Any(c => c.ParentId == n.Id)
            })
            .ToListAsync(ct);

        return rows.Select(r => r.Node.ToDto(r.HasChildren)).ToList();
    }

    public async Task<List<StringNodeDto>> GetChildrenAsync(int parentId, CancellationToken ct = default)
    {
        var rows = await _db.StringNodes
            .Where(n => n.ParentId == parentId)
            .OrderBy(n => n.SortOrder).ThenBy(n => n.Id)
            .Select(n => new
            {
                Node = n,
                HasChildren = _db.StringNodes.Any(c => c.ParentId == n.Id)
            })
            .ToListAsync(ct);

        return rows.Select(r => r.Node.ToDto(r.HasChildren)).ToList();
    }

    public async Task<StringNodeDto?> GetNodeAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.StringNodes.FindAsync(new object?[] { id }, ct);
        if (entity == null) return null;
        var hasChildren = await _db.StringNodes.AnyAsync(c => c.ParentId == id, ct);
        return entity.ToDto(hasChildren);
    }

    public async Task<List<StringNodeDto>> GetAncestorPathAsync(int id, CancellationToken ct = default)
    {
        var path = new List<StringNodeDto>();
        var current = await _db.StringNodes.FindAsync(new object?[] { id }, ct);
        while (current != null)
        {
            var hasChildren = await _db.StringNodes.AnyAsync(c => c.ParentId == current.Id, ct);
            path.Insert(0, current.ToDto(hasChildren));
            if (!current.ParentId.HasValue) break;
            current = await _db.StringNodes.FindAsync(new object?[] { current.ParentId.Value }, ct);
        }
        return path;
    }

    public async Task<StringNodeDto> CreateNodeAsync(StringNodeDto dto, CancellationToken ct = default)
    {
        var entity = new StringNodeEntity
        {
            Name = dto.Name?.Trim() ?? "未命名",
            ParentId = dto.ParentId,
            Description = dto.Description,
            SortOrder = await NextSortOrderAsync(dto.ParentId, ct)
        };
        _db.StringNodes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.ToDto(false);
    }

    public async Task<StringNodeDto?> UpdateNodeAsync(int id, StringNodeDto dto, CancellationToken ct = default)
    {
        var entity = await _db.StringNodes.FindAsync(new object?[] { id }, ct);
        if (entity == null) return null;

        entity.Name = dto.Name?.Trim() ?? entity.Name;
        entity.Description = dto.Description;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        var hasChildren = await _db.StringNodes.AnyAsync(c => c.ParentId == id, ct);
        return entity.ToDto(hasChildren);
    }

    public async Task<bool> DeleteNodeAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.StringNodes.FindAsync(new object?[] { id }, ct);
        if (entity == null) return false;

        await DeleteRecursiveAsync(id, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task DeleteRecursiveAsync(int id, CancellationToken ct)
    {
        var children = await _db.StringNodes.Where(n => n.ParentId == id).ToListAsync(ct);
        foreach (var child in children)
        {
            await DeleteRecursiveAsync(child.Id, ct);
        }
        var entity = await _db.StringNodes.FindAsync(new object?[] { id }, ct);
        if (entity != null) _db.StringNodes.Remove(entity);
    }

    public async Task<bool> MoveNodeAsync(int id, int? newParentId, int newSortOrder, CancellationToken ct = default)
    {
        if (id == newParentId) return false;

        var entity = await _db.StringNodes.FindAsync(new object?[] { id }, ct);
        if (entity == null) return false;

        if (newParentId.HasValue && await WouldCreateCycleAsync(id, newParentId.Value, ct))
            return false;

        entity.ParentId = newParentId;
        entity.SortOrder = newSortOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> WouldCreateCycleAsync(int nodeId, int newParentId, CancellationToken ct)
    {
        var currentId = (int?)newParentId;
        var guard = 0;
        while (currentId.HasValue && guard++ < 1000)
        {
            if (currentId.Value == nodeId) return true;
            var parent = await _db.StringNodes.FindAsync(new object?[] { currentId.Value }, ct);
            if (parent == null) break;
            currentId = parent.ParentId;
        }
        return false;
    }

    public async Task<bool> SortChildrenAsync(int parentId, IReadOnlyList<int> orderedIds, CancellationToken ct = default)
    {
        var children = await _db.StringNodes.Where(n => n.ParentId == parentId).ToListAsync(ct);
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

    private async Task<int> NextSortOrderAsync(int? parentId, CancellationToken ct)
    {
        var max = await _db.StringNodes
            .Where(n => n.ParentId == parentId)
            .Select(n => (int?)n.SortOrder)
            .MaxAsync(ct);
        return (max ?? -1) + 1;
    }
}

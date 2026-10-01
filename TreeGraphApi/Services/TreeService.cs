using APromisedLand.Shared.TreeGraph.Models;
using TreeGraphApi.Entities;
using TreeGraphApi.Repositories;

namespace TreeGraphApi.Services;

public class TreeService : ITreeService
{
    private readonly ITreeRepository _repo;

    public TreeService(ITreeRepository repo) => _repo = repo;

    public async Task<IReadOnlyCollection<TreeNodeDto>> GetRootsAsync(TreeQueryOptions opts, CancellationToken ct = default)
    {
        var entities = await _repo.GetRootsAsync(opts.Skip, opts.Take, ct);
        return entities.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyCollection<TreeNodeDto>> GetChildrenAsync(string parentId, CancellationToken ct = default)
    {
        var entities = await _repo.GetChildrenAsync(parentId, ct);
        return entities.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyCollection<TreeNodeDto>> GetSubtreeAsync(string rootId, CancellationToken ct = default)
    {
        var entities = await _repo.GetSubtreeAsync(rootId, ct);
        return entities.Select(ToDto).ToList();
    }

    public async Task<TreeNodeDto> CreateAsync(TreeNodeDto dto, CancellationToken ct = default)
    {
        var entity = ToEntity(dto);
        // 新增节点时按 ParentId 推断 HasChildren=false;父节点 HasChildren 由仓储层维护
        entity.HasChildren = false;
        var created = await _repo.AddAsync(entity, ct);
        return ToDto(created);
    }

    /// <summary>
    /// 优化 #7:UpdateAsync 走乐观并发,EF Core 用 PostgreSQL xmin 自动管理 RowVersion,
    /// SaveChanges 时 WHERE RowVersion = ? 检查,失败抛 DbUpdateConcurrencyException,
    /// 由 Controller 转换为 409 Conflict,避免后写覆盖。
    /// </summary>
    public async Task<TreeNodeDto> UpdateAsync(string id, TreeNodeDto dto, CancellationToken ct = default)
    {
        var existing = await _repo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Node {id} not found");

        // RowVersion 由 EF Core 通过 xmin 自动管理,客户端不传;
        // existing 已被 EF 跟踪,SaveChanges 时自动加入 WHERE RowVersion 检查

        existing.Text = dto.Text;
        existing.Icon = dto.Icon;
        existing.NodeType = dto.NodeType;
        existing.SortOrder = dto.SortOrder;
        existing.HasChildren = dto.HasChildren;

        var updated = await _repo.UpdateAsync(existing, ct);
        return ToDto(updated);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
        => await _repo.DeleteSubtreeAsync(id, ct);

    public async Task MoveAsync(string nodeId, string newParentId, CancellationToken ct = default)
        => await _repo.MoveAsync(nodeId, newParentId, ct);

    // —— 映射函数 ——

    private static TreeNodeDto ToDto(TreeNodeEntity e) => new()
    {
        Id = e.Id,
        ParentId = e.ParentId,
        Text = e.Text,
        Icon = e.Icon,
        SortOrder = e.SortOrder,
        HasChildren = e.HasChildren,
        NodeType = e.NodeType,
        RowVersion = BitConverter.GetBytes(e.RowVersion)
    };

    private static TreeNodeEntity ToEntity(TreeNodeDto d) => new()
    {
        Id = d.Id,
        ParentId = d.ParentId,
        Text = d.Text,
        Icon = d.Icon,
        NodeType = d.NodeType,
        SortOrder = d.SortOrder,
        HasChildren = d.HasChildren,
    };
}

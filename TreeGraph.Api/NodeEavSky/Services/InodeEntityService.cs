using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.NodeEavSky.Services;

public interface IInodeEntityService
{
    // ============================================================
    // 绑定层（一条绑定 = 一个名字 + 一条私有 EAV 数据）
    // ============================================================

    /// <summary>列出某 iNode 下的全部命名绑定。</summary>
    Task<IReadOnlyList<InodeEntity>> ListBindingsAsync(
        string inodeId, CancellationToken ct = default);

    /// <summary>列出某 iNode 在某类型下的全部命名绑定。</summary>
    Task<IReadOnlyList<InodeEntity>> ListBindingsAsync(
        string inodeId, string entityType, CancellationToken ct = default);

    /// <summary>按 binding_id 取绑定；inode 不匹配或不存在时返回 null。</summary>
    Task<InodeEntity?> GetBindingAsync(
        string inodeId, string bindingId, CancellationToken ct = default);

    /// <summary>新建命名绑定（生成 binding_id 与该绑定私有的 entity_id）。</summary>
    Task<(string BindingId, string EntityId)> CreateBindingAsync(
        string inodeId, string entityType, string name, CancellationToken ct = default);

    /// <summary>重命名绑定；名字在该 (inode, type) 下必须唯一。</summary>
    Task<bool> RenameBindingAsync(
        string inodeId, string bindingId, string newName, CancellationToken ct = default);

    /// <summary>更新绑定显示顺序（仅改排序值，不做连续性校验）。</summary>
    Task<bool> UpdateBindingOrderAsync(
        string inodeId, string bindingId, int displayOrder, CancellationToken ct = default);

    /// <summary>删除绑定行（EAV 数据的删除由 Facade 负责）。</summary>
    Task<bool> DeleteBindingAsync(
        string inodeId, string bindingId, CancellationToken ct = default);

    /// <summary>列出某类型名下所有绑定（跨 iNode 查询限定候选集用）。</summary>
    Task<IReadOnlyList<InodeEntity>> ListByTypeAsync(
        string entityType, CancellationToken ct = default);

    // ============================================================
    // 声明层
    // ============================================================

    Task<IReadOnlyList<InodeEntityType>> ListDeclarationsAsync(
        string inodeId, CancellationToken ct = default);

    Task AttachAsync(
        string inodeId, string entityType, CancellationToken ct = default);

    Task<bool> DetachAsync(
        string inodeId, string entityType, CancellationToken ct = default);
}

/// <summary>
/// iNode ↔ 实体绑定服务。
///
/// 不触碰任何 EAV 表，只操作 inode_entitytype / inode_entity。
/// inode_entity 为「命名绑定」表：每条绑定拥有独立名字与独立 entity_id，
/// 因此同一 iNode 同一类型可建多条绑定，绑定之间数据互不共享。
/// </summary>
public class InodeEntityService : IInodeEntityService
{
    private readonly TreeGraphDbContext _db;
    private readonly InodeOwnerGuardRegistry _ownerGuards;

    public InodeEntityService(
        TreeGraphDbContext db,
        InodeOwnerGuardRegistry ownerGuards)
    {
        _db = db;
        _ownerGuards = ownerGuards;
    }

    // ============================================================
    // 绑定层
    // ============================================================

    public async Task<IReadOnlyList<InodeEntity>> ListBindingsAsync(
        string inodeId, CancellationToken ct = default)
    {
        return await _db.InodeEntities
            .Where(x => x.InodeId == inodeId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.AttachedAt)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<InodeEntity>> ListBindingsAsync(
        string inodeId, string entityType, CancellationToken ct = default)
    {
        return await _db.InodeEntities
            .Where(x => x.InodeId == inodeId && x.EntityType == entityType)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.AttachedAt)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<InodeEntity?> GetBindingAsync(
        string inodeId, string bindingId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bindingId)) return null;

        return await _db.InodeEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.BindingId == bindingId && x.InodeId == inodeId, ct);
    }

    public async Task<(string BindingId, string EntityId)> CreateBindingAsync(
        string inodeId, string entityType, string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(inodeId))
            throw new ArgumentException("inodeId 不能为空", nameof(inodeId));
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("entityType 不能为空", nameof(entityType));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("绑定名不能为空", nameof(name));

        var trimmed = name.Trim();

        // 宿主存在性：inodeId 必须是真实节点（树节点守卫等）
        await _ownerGuards.EnsureNodeExistsAsync(inodeId, ct);

        // 同一 iNode 同一类型下绑定名唯一
        var duplicated = await _db.InodeEntities.AnyAsync(
            x => x.InodeId == inodeId && x.EntityType == entityType && x.Name == trimmed, ct);
        if (duplicated)
            throw new InvalidOperationException(
                $"类型「{entityType}」下已存在名为「{trimmed}」的绑定，请换一个名称");

        var now = DateTimeOffset.UtcNow;
        var bindingId = Guid.NewGuid().ToString("D");
        var entityId = Guid.NewGuid().ToString("D");

        // 显示顺序：现有条目数 + 1（与属性定义新建规则一致）
        var displayOrder = await _db.InodeEntities
            .Where(x => x.InodeId == inodeId)
            .MaxAsync(x => (int?)x.DisplayOrder, ct) + 1 ?? 1;

        _db.InodeEntities.Add(new InodeEntity
        {
            BindingId = bindingId,
            Name = trimmed,
            InodeId = inodeId,
            EntityType = entityType,
            EntityId = entityId,
            DisplayOrder = displayOrder,
            AttachedAt = now
        });

        await EnsureDeclarationAsync(inodeId, entityType, now, ct);
        await _db.SaveChangesAsync(ct);
        return (bindingId, entityId);
    }

    public async Task<bool> RenameBindingAsync(
        string inodeId, string bindingId, string newName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("绑定名不能为空", nameof(newName));

        var trimmed = newName.Trim();

        var binding = await _db.InodeEntities.FirstOrDefaultAsync(
            x => x.BindingId == bindingId && x.InodeId == inodeId, ct);
        if (binding is null) return false;
        if (binding.Name == trimmed) return true;

        var duplicated = await _db.InodeEntities.AnyAsync(
            x => x.InodeId == inodeId
                 && x.EntityType == binding.EntityType
                 && x.Name == trimmed
                 && x.BindingId != bindingId, ct);
        if (duplicated)
            throw new InvalidOperationException(
                $"类型「{binding.EntityType}」下已存在名为「{trimmed}」的绑定，请换一个名称");

        binding.Name = trimmed;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateBindingOrderAsync(
        string inodeId, string bindingId, int displayOrder, CancellationToken ct = default)
    {
        var binding = await _db.InodeEntities.FirstOrDefaultAsync(
            x => x.BindingId == bindingId && x.InodeId == inodeId, ct);
        if (binding is null) return false;

        binding.DisplayOrder = displayOrder;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteBindingAsync(
        string inodeId, string bindingId, CancellationToken ct = default)
    {
        var binding = await _db.InodeEntities.FirstOrDefaultAsync(
            x => x.BindingId == bindingId && x.InodeId == inodeId, ct);
        if (binding is null) return false;

        _db.InodeEntities.Remove(binding);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<InodeEntity>> ListByTypeAsync(
        string entityType, CancellationToken ct = default)
    {
        return await _db.InodeEntities
            .Where(x => x.EntityType == entityType)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    // ============================================================
    // 声明层
    // ============================================================

    public async Task<IReadOnlyList<InodeEntityType>> ListDeclarationsAsync(
        string inodeId, CancellationToken ct = default)
    {
        return await _db.InodeEntityTypes
            .Where(x => x.InodeId == inodeId)
            .OrderBy(x => x.EntityType)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task AttachAsync(
        string inodeId, string entityType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(inodeId))
            throw new ArgumentException("inodeId 不能为空", nameof(inodeId));
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("entityType 不能为空", nameof(entityType));

        // 幂等
        var exists = await _db.InodeEntityTypes
            .AnyAsync(x => x.InodeId == inodeId && x.EntityType == entityType, ct);
        if (exists) return;

        // 宿主存在性：只有真实节点才能关注实体类型
        await _ownerGuards.EnsureNodeExistsAsync(inodeId, ct);

        _db.InodeEntityTypes.Add(new InodeEntityType
        {
            InodeId = inodeId,
            EntityType = entityType,
            AttachedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DetachAsync(
        string inodeId, string entityType, CancellationToken ct = default)
    {
        // 仍有绑定存在时拒绝，避免留下悬空绑定
        var hasBinding = await _db.InodeEntities
            .AnyAsync(x => x.InodeId == inodeId && x.EntityType == entityType, ct);
        if (hasBinding)
            throw new InvalidOperationException(
                "该 iNode 下仍存在该类型的绑定，请先删除全部绑定后再取消声明");

        var link = await _db.InodeEntityTypes
            .FirstOrDefaultAsync(
                x => x.InodeId == inodeId && x.EntityType == entityType, ct);
        if (link is null) return false;

        _db.InodeEntityTypes.Remove(link);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>绑定存在时必须同时存在类型声明（混合模式：自动补声明）。</summary>
    private async Task EnsureDeclarationAsync(
        string inodeId, string entityType, DateTimeOffset now, CancellationToken ct)
    {
        var declared = await _db.InodeEntityTypes.AnyAsync(
            x => x.InodeId == inodeId && x.EntityType == entityType, ct);
        if (declared) return;

        _db.InodeEntityTypes.Add(new InodeEntityType
        {
            InodeId = inodeId,
            EntityType = entityType,
            AttachedAt = now
        });
    }
}

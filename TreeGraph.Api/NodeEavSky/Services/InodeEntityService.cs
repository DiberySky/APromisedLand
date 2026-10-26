using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.NodeEavSky.Services;

public interface IInodeEntityService
{
    /// <summary>查归属映射，返回 entity_id（不存在返回 null）。</summary>
    Task<string?> GetEntityIdAsync(
        string inodeId, string entityType, CancellationToken ct = default);

    /// <summary>
    /// 获取或创建 entity_id（原子）。
    /// 不存在则生成新 GUID 并写映射 + 声明。
    /// </summary>
    Task<string> GetOrCreateEntityIdAsync(
        string inodeId, string entityType, CancellationToken ct = default);

    /// <summary>列出某 iNode 下所有归属映射。</summary>
    Task<IReadOnlyList<InodeEntity>> ListByInodeAsync(
        string inodeId, CancellationToken ct = default);

    /// <summary>按类型名反查 iNode（entity 只属于一个 iNode）。</summary>
    Task<InodeEntity?> GetByEntityAsync(
        string entityType, string entityId, CancellationToken ct = default);

    /// <summary>删除归属映射（不影响 EAV 数据）。</summary>
    Task<bool> DeleteMappingAsync(
        string inodeId, string entityType, CancellationToken ct = default);

    /// <summary>
    /// 转移实体归属：把 (sourceInodeId, entityType) 的实体改挂到 targetInodeId。
    /// 只改映射，不触碰 EAV 数据与声明（目标侧自动补声明，与 GetOrCreate 混合模式一致）。
    /// 返回更新后的映射；源映射不存在返回 null；
    /// 目标宿主不存在 / 目标已有同类型实体 → 抛异常。
    /// </summary>
    Task<InodeEntity?> TransferAsync(
        string sourceInodeId, string entityType, string targetInodeId,
        CancellationToken ct = default);

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
/// iNode ↔ 实体归属服务。
///
/// 不触碰任何 EAV 表，只操作 inode_entitytype / inode_entity。
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
    // 归属层
    // ============================================================

    public async Task<string?> GetEntityIdAsync(
        string inodeId, string entityType, CancellationToken ct = default)
    {
        return await _db.InodeEntities
            .Where(x => x.InodeId == inodeId && x.EntityType == entityType)
            .Select(x => x.EntityId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string> GetOrCreateEntityIdAsync(
        string inodeId, string entityType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(inodeId))
            throw new ArgumentException("inodeId 不能为空", nameof(inodeId));
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("entityType 不能为空", nameof(entityType));

        // 1. 查现有映射
        var existing = await GetEntityIdAsync(inodeId, entityType, ct);
        if (existing is not null) return existing;

        // 1.5 宿主存在性：inodeId 必须是真实节点（树节点守卫等）
        await _ownerGuards.EnsureNodeExistsAsync(inodeId, ct);

        // 2. 生成 entity_id（与现有 EAV 用法一致，GUID 字符串）
        var entityId = Guid.NewGuid().ToString("D");
        var now = DateTimeOffset.UtcNow;

        // 3. 写归属映射
        _db.InodeEntities.Add(new InodeEntity
        {
            InodeId = inodeId,
            EntityType = entityType,
            EntityId = entityId,
            AttachedAt = now
        });

        // 5. 自动补声明（混合模式）
        var declared = await _db.InodeEntityTypes.AnyAsync(
            x => x.InodeId == inodeId && x.EntityType == entityType, ct);
        if (!declared)
        {
            _db.InodeEntityTypes.Add(new InodeEntityType
            {
                InodeId = inodeId,
                EntityType = entityType,
                AttachedAt = now
            });
        }

        await _db.SaveChangesAsync(ct);
        return entityId;
    }

    public async Task<IReadOnlyList<InodeEntity>> ListByInodeAsync(
        string inodeId, CancellationToken ct = default)
    {
        return await _db.InodeEntities
            .Where(x => x.InodeId == inodeId)
            .OrderBy(x => x.EntityType)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<InodeEntity?> GetByEntityAsync(
        string entityType, string entityId, CancellationToken ct = default)
    {
        return await _db.InodeEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.EntityType == entityType && x.EntityId == entityId, ct);
    }

    public async Task<bool> DeleteMappingAsync(
        string inodeId, string entityType, CancellationToken ct = default)
    {
        var mapping = await _db.InodeEntities
            .FirstOrDefaultAsync(
                x => x.InodeId == inodeId && x.EntityType == entityType, ct);
        if (mapping is null) return false;

        _db.InodeEntities.Remove(mapping);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<InodeEntity?> TransferAsync(
        string sourceInodeId, string entityType, string targetInodeId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetInodeId))
            throw new ArgumentException("targetInodeId 不能为空", nameof(targetInodeId));
        if (sourceInodeId == targetInodeId)
            throw new InvalidOperationException("目标宿主与当前宿主相同，无需转移");

        var mapping = await _db.InodeEntities
            .FirstOrDefaultAsync(
                x => x.InodeId == sourceInodeId && x.EntityType == entityType, ct);
        if (mapping is null) return null;

        // 目标宿主存在性：必须挂到真实节点上
        await _ownerGuards.EnsureNodeExistsAsync(targetInodeId, ct);

        // 目标已有同类型实体 → 拒绝（一宿主一类型一实体）
        var occupied = await _db.InodeEntities
            .AnyAsync(x => x.InodeId == targetInodeId && x.EntityType == entityType, ct);
        if (occupied)
            throw new InvalidOperationException(
                $"目标宿主已存在 {entityType} 类型的实体，无法转移");

        // InodeId 是复合主键的一部分，EF Core 不允许改键：删旧映射 + 加新映射
        var entityId = mapping.EntityId;
        _db.InodeEntities.Remove(mapping);
        var newMapping = new InodeEntity
        {
            InodeId = targetInodeId,
            EntityType = entityType,
            EntityId = entityId,
            AttachedAt = DateTimeOffset.UtcNow
        };
        _db.InodeEntities.Add(newMapping);

        // 目标侧自动补声明（混合模式，与 GetOrCreateEntityId 一致）
        var declared = await _db.InodeEntityTypes.AnyAsync(
            x => x.InodeId == targetInodeId && x.EntityType == entityType, ct);
        if (!declared)
        {
            _db.InodeEntityTypes.Add(new InodeEntityType
            {
                InodeId = targetInodeId,
                EntityType = entityType,
                AttachedAt = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        return newMapping;
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
        // 已有实体时拒绝
        var hasEntity = await _db.InodeEntities
            .AnyAsync(x => x.InodeId == inodeId && x.EntityType == entityType, ct);
        if (hasEntity)
            throw new InvalidOperationException(
                "该 iNode 下已有此类型的实体，请先删除实体后再取消声明");

        var link = await _db.InodeEntityTypes
            .FirstOrDefaultAsync(
                x => x.InodeId == inodeId && x.EntityType == entityType, ct);
        if (link is null) return false;

        _db.InodeEntityTypes.Remove(link);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

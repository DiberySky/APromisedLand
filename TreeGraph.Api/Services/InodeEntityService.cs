using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;

namespace TreeGraph.Api.Services;

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
    private readonly EavDbContext _db;

    public InodeEntityService(EavDbContext db)
    {
        _db = db;
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

        // 2. 类型存在性（查 entity_type_catalog）
        if (!await TypeExistsAsync(entityType, ct))
            throw new InvalidOperationException($"实体类型不存在: {entityType}");

        // 3. 生成 entity_id（与现有 EAV 用法一致，GUID 字符串）
        var entityId = Guid.NewGuid().ToString("D");
        var now = DateTimeOffset.UtcNow;

        // 4. 写归属映射
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

        // 类型存在性（查 entity_type_catalog）
        if (!await TypeExistsAsync(entityType, ct))
            throw new InvalidOperationException($"实体类型不存在: {entityType}");

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

    // ============================================================
    // 内部辅助：类型存在性检查
    // ============================================================

    /// <summary>
    /// 类型存在性检查（查 entity_type_catalog，不是 attribute_catalog）。
    ///
    /// ★ 之前的 bug：误查 attribute_catalog，导致"无属性的类型"被判定为不存在。
    ///   语义上，类型是否存在于 entity_type_catalog 与是否有属性无关。
    ///
    ///   例外场景（允许"无属性类型"的操作）：
    ///     - 声明（Attach）：允许
    ///     - 创建实体（GetOrCreateEntityId）：允许，但实际写入时若无属性会被
    ///       未知属性检查拒绝，等同"写不进任何东西"
    /// </summary>
    private async Task<bool> TypeExistsAsync(
        string entityType, CancellationToken ct)
    {
        return await _db.EntityTypes
            .AnyAsync(t => t.EntityType == entityType && !t.IsDeleted, ct);
    }
}

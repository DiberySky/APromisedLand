using TreeGraph.Api.MessagingSky;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.Messaging;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>
/// iNode 视角 → EAV 管道的转发层。
///
/// 职责：
///   1. 从 (inodeId, entityType) 解析/生成 entityId
///   2. 调用现有 EAV 服务（签名完全不变）
///   3. 删除时级联清理 inode_entity 映射
///
/// 不做验证/写库逻辑，纯转发，便于维护。
/// </summary>
public class InodeEavFacade
{
    private readonly IInodeEntityService _inodeEntities;
    private readonly EavWriteService _write;
    private readonly EavReadService _read;
    private readonly IEventPublisher _publishEndpoint;

    public InodeEavFacade(
        IInodeEntityService inodeEntities,
        EavWriteService write,
        EavReadService read,
        IEventPublisher publishEndpoint)
    {
        _inodeEntities = inodeEntities;
        _write = write;
        _read = read;
        _publishEndpoint = publishEndpoint;
    }

    // ============================================================
    // 读取
    // ============================================================

    /// <summary>
    /// 按 (inodeId, entityType) 读实体。
    /// iNode 下该类型尚未创建实体时返回 null。
    /// </summary>
    public async Task<DynamicEntity?> LoadAsync(
        string inodeId, string entityType,
        CancellationToken ct = default)
    {
        var entityId = await _inodeEntities.GetEntityIdAsync(
            inodeId, entityType, ct);
        if (entityId is null) return null;

        return await _read.LoadAsync(entityId, entityType, ct);
    }

    /// <summary>审计历史。</summary>
    public async Task<List<AttributeAuditLog>> GetHistoryAsync(
        string inodeId, string entityType,
        DateTimeOffset? from = null,
        CancellationToken ct = default)
    {
        var entityId = await _inodeEntities.GetEntityIdAsync(
            inodeId, entityType, ct);
        if (entityId is null) return new List<AttributeAuditLog>();

        return await _read.GetHistoryAsync(entityId, from, ct);
    }

    // ============================================================
    // 写入
    // ============================================================

    public async Task SaveAsync(
        string inodeId, string entityType,
        Dictionary<string, object?> values,
        string changedBy, string? correlationId = null,
        CancellationToken ct = default,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        var entityId = await _inodeEntities.GetOrCreateEntityIdAsync(
            inodeId, entityType, ct);

        await _write.SaveAsync(entityId, entityType, values,
            changedBy, correlationId, ct, expectedUpdatedAt);

        // ★ 语义搜索：发布宿主变更事件（at-least-once，消费端幂等）
        await _publishEndpoint.Publish(new InodeChangedEvent
        {
            InodeId = inodeId,
            Kind = InodeChangeKind.Saved
        }, ct);
    }

    public async Task PatchAsync(
        string inodeId, string entityType,
        Dictionary<string, object?> values,
        string changedBy, string? correlationId = null,
        CancellationToken ct = default,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        var entityId = await _inodeEntities.GetOrCreateEntityIdAsync(
            inodeId, entityType, ct);

        await _write.PatchAsync(entityId, entityType, values,
            changedBy, correlationId, ct, expectedUpdatedAt);

        // ★ 语义搜索：发布宿主变更事件
        await _publishEndpoint.Publish(new InodeChangedEvent
        {
            InodeId = inodeId,
            Kind = InodeChangeKind.Patched
        }, ct);
    }

    /// <summary>
    /// 删除实体：先删 EAV 数据，再删归属映射。
    /// </summary>
    public async Task<bool> DeleteAsync(
        string inodeId, string entityType,
        string changedBy, string? correlationId = null,
        CancellationToken ct = default)
    {
        var entityId = await _inodeEntities.GetEntityIdAsync(
            inodeId, entityType, ct);
        if (entityId is null) return false;

        // 1. 删 EAV 数据（属性值 + 审计 + 子表行）
        await _write.DeleteEntityAsync(
            entityId, entityType, changedBy, correlationId, ct);

        // 2. 删归属映射
        await _inodeEntities.DeleteMappingAsync(inodeId, entityType, ct);

        // ★ 语义搜索：发布宿主变更事件（消费端据此刷新/删除向量）
        await _publishEndpoint.Publish(new InodeChangedEvent
        {
            InodeId = inodeId,
            Kind = InodeChangeKind.Deleted
        }, ct);

        return true;
    }

    // ============================================================
    // 便利方法：加载某 iNode 下所有实体
    // ============================================================

    /// <summary>
    /// 加载某 iNode 下所有已创建实体，按类型名分组返回。
    /// 未创建的类型不会出现在结果里。
    /// </summary>
    public async Task<Dictionary<string, DynamicEntity>> LoadAllByInodeAsync(
        string inodeId,
        CancellationToken ct = default)
    {
        var mappings = await _inodeEntities.ListByInodeAsync(inodeId, ct);
        if (mappings.Count == 0)
            return new Dictionary<string, DynamicEntity>();

        var result = new Dictionary<string, DynamicEntity>();
        foreach (var m in mappings)
        {
            var entity = await _read.LoadAsync(
                m.EntityId, m.EntityType, ct);
            result[m.EntityType] = entity;
        }
        return result;
    }
}

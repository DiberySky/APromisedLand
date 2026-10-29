using TreeGraph.Api.MessagingSky;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.Messaging;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>
/// iNode 视角 → EAV 管道的转发层。
///
/// 职责：
///   1. 以「命名绑定」为单位读写：binding_id → (entity_type, entity_id)
///   2. 调用现有 EAV 服务（签名完全不变）
///   3. 删除绑定 = 删绑定行 + 删该绑定私有的 EAV 数据
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
    /// 按 (inodeId, bindingId) 读绑定对应的实体。
    /// 绑定不存在或不属于该 iNode 时返回 null。
    /// </summary>
    public async Task<DynamicEntity?> LoadAsync(
        string inodeId, string bindingId,
        CancellationToken ct = default)
    {
        var binding = await _inodeEntities.GetBindingAsync(inodeId, bindingId, ct);
        if (binding is null) return null;

        return await _read.LoadAsync(binding.EntityId, binding.EntityType, ct);
    }

    /// <summary>按绑定读审计历史。</summary>
    public async Task<List<AttributeAuditLog>> GetHistoryAsync(
        string inodeId, string bindingId,
        DateTimeOffset? from = null,
        CancellationToken ct = default)
    {
        var binding = await _inodeEntities.GetBindingAsync(inodeId, bindingId, ct);
        if (binding is null) return new List<AttributeAuditLog>();

        return await _read.GetHistoryAsync(binding.EntityId, from, ct);
    }

    // ============================================================
    // 写入
    // ============================================================

    /// <summary>新建命名绑定（含该绑定私有的实体）。</summary>
    public Task<(string BindingId, string EntityId)> CreateBindingAsync(
        string inodeId, string entityType, string name,
        CancellationToken ct = default)
        => _inodeEntities.CreateBindingAsync(inodeId, entityType, name, ct);

    public Task<bool> RenameBindingAsync(
        string inodeId, string bindingId, string newName,
        CancellationToken ct = default)
        => _inodeEntities.RenameBindingAsync(inodeId, bindingId, newName, ct);

    public Task<bool> ReorderBindingAsync(
        string inodeId, string bindingId, int displayOrder,
        CancellationToken ct = default)
        => _inodeEntities.UpdateBindingOrderAsync(inodeId, bindingId, displayOrder, ct);

    public async Task SaveAsync(
        string inodeId, string bindingId,
        Dictionary<string, object?> values,
        string changedBy, string? correlationId = null,
        CancellationToken ct = default,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        var binding = await RequireBindingAsync(inodeId, bindingId, ct);

        await _write.SaveAsync(binding.EntityId, binding.EntityType, values,
            changedBy, correlationId, ct, expectedUpdatedAt);

        await PublishAsync(inodeId, InodeChangeKind.Saved, ct);
    }

    public async Task PatchAsync(
        string inodeId, string bindingId,
        Dictionary<string, object?> values,
        string changedBy, string? correlationId = null,
        CancellationToken ct = default,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        var binding = await RequireBindingAsync(inodeId, bindingId, ct);

        await _write.PatchAsync(binding.EntityId, binding.EntityType, values,
            changedBy, correlationId, ct, expectedUpdatedAt);

        await PublishAsync(inodeId, InodeChangeKind.Patched, ct);
    }

    /// <summary>删除绑定：删该绑定私有的 EAV 数据 + 删绑定行。</summary>
    public async Task<bool> DeleteAsync(
        string inodeId, string bindingId,
        string changedBy, string? correlationId = null,
        CancellationToken ct = default)
    {
        var binding = await _inodeEntities.GetBindingAsync(inodeId, bindingId, ct);
        if (binding is null) return false;

        await _write.DeleteEntityAsync(
            binding.EntityId, binding.EntityType, changedBy, correlationId, ct);

        await _inodeEntities.DeleteBindingAsync(inodeId, bindingId, ct);

        await PublishAsync(inodeId, InodeChangeKind.Deleted, ct);
        return true;
    }

    // ============================================================
    // 便利方法：加载某 iNode 下所有绑定的实体
    // ============================================================

    /// <summary>加载某 iNode 下所有绑定对应的实体（同类型可多条）。</summary>
    public async Task<IReadOnlyList<DynamicEntity>> LoadAllByInodeAsync(
        string inodeId,
        CancellationToken ct = default)
    {
        var bindings = await _inodeEntities.ListBindingsAsync(inodeId, ct);
        if (bindings.Count == 0) return Array.Empty<DynamicEntity>();

        var result = new List<DynamicEntity>(bindings.Count);
        foreach (var b in bindings)
        {
            var entity = await _read.LoadAsync(b.EntityId, b.EntityType, ct);
            if (entity is not null) result.Add(entity);
        }
        return result;
    }

    // ============================================================
    // 内部
    // ============================================================

    private async Task<InodeEntity> RequireBindingAsync(
        string inodeId, string bindingId, CancellationToken ct)
    {
        var binding = await _inodeEntities.GetBindingAsync(inodeId, bindingId, ct);
        if (binding is null)
            throw new InvalidOperationException(
                $"节点 {inodeId} 下不存在绑定 {bindingId}");
        return binding;
    }

    private Task PublishAsync(
        string inodeId, InodeChangeKind kind, CancellationToken ct)
        => _publishEndpoint.Publish(new InodeChangedEvent
        {
            InodeId = inodeId,
            Kind = kind
        }, ct);
}

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>
/// 实体归属守卫：对“受外部主表约束”的实体类型，在任何 EAV 写入前
/// 校验目标 entityId 在其宿主主表中真实存在，防止属性值脱离宿主实体
/// 凭空生成（孤儿数据）。
///
/// 一个实体类型可由零个或多个模块认领；未被任何守卫认领的类型直接放行
/// （例如 Product/item 等通用动态实体）。
/// </summary>
public interface IEntityOwnerGuard
{
    /// <summary>
    /// 校验 (entityType, entityId) 的宿主存在性。
    /// 宿主不存在时应抛出 <see cref="EavValidationException"/>，
    /// 由现有控制器统一映射为 400。
    /// </summary>
    Task EnsureOwnerExistsAsync(
        string entityType, string entityId, CancellationToken ct = default);
}

/// <summary>聚合所有已注册的 <see cref="IEntityOwnerGuard"/>，供写服务单点调用。</summary>
public sealed class EntityOwnerGuardRegistry
{
    private readonly IReadOnlyList<IEntityOwnerGuard> _guards;

    public EntityOwnerGuardRegistry(IEnumerable<IEntityOwnerGuard> guards)
        => _guards = guards as IReadOnlyList<IEntityOwnerGuard> ?? guards.ToList();

    public async Task EnsureOwnerExistsAsync(
        string entityType, string entityId, CancellationToken ct = default)
    {
        foreach (var guard in _guards)
            await guard.EnsureOwnerExistsAsync(entityType, entityId, ct);
    }
}

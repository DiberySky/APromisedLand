using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>
/// iNode 宿主守卫：在节点关注实体类型 / 首次写入实体数据前，
/// 校验 inodeId 在其宿主主表（如树节点表）中真实存在，
/// 防止属性数据脱离宿主节点凭空生成（孤儿数据）。
///
/// 与 <see cref="IEntityOwnerGuard"/> 的区别：
///   - 旧模型：entityId 直接等于宿主主键，按 entityType 分流校验；
///   - iNode 模型：inodeId 是宿主（节点），entityId 由服务端独立生成，
///     校验对象是 inodeId 本身。
///
/// 一个 inodeId 可被任一守卫认领（例如它可能是单位树节点或分类树节点）；
/// 未注册任何守卫时直接放行（inode 由外部应用维护的场景）。
/// </summary>
public interface IInodeOwnerGuard
{
    /// <summary>判断 inodeId 是否为本守卫所维护宿主表中的真实节点。</summary>
    Task<bool> NodeExistsAsync(string inodeId, CancellationToken ct = default);
}

/// <summary>
/// 聚合所有已注册的 <see cref="IInodeOwnerGuard"/>，供 inode 写服务单点调用。
/// 任一守卫确认节点存在即通过；无守卫注册时视为外部模式，放行。
/// </summary>
public sealed class InodeOwnerGuardRegistry
{
    private readonly IReadOnlyList<IInodeOwnerGuard> _guards;

    public InodeOwnerGuardRegistry(IEnumerable<IInodeOwnerGuard> guards)
        => _guards = guards as IReadOnlyList<IInodeOwnerGuard> ?? guards.ToList();

    public async Task EnsureNodeExistsAsync(
        string inodeId, CancellationToken ct = default)
    {
        // 外部模式：未注册任何宿主表守卫时不拦截
        if (_guards.Count == 0) return;

        foreach (var guard in _guards)
        {
            if (await guard.NodeExistsAsync(inodeId, ct))
                return;
        }

        throw new EavValidationException(
        [
            new ValidationError("inodeId",
                $"宿主节点 '{inodeId}' 不存在，无法关注实体类型或写入其属性。")
        ]);
    }
}

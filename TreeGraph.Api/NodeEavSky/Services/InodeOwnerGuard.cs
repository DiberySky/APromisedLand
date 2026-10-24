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
    /// <summary>
    /// 本守卫认领的宿主节点类型名（与宿主类名 / EAV entity_type 一致，如 "UnitTree"）。
    /// 供需要按类型精确分流校验的场景使用（如 EdgeSky 起点终点节点校验）。
    /// </summary>
    string NodeType { get; }

    /// <summary>判断 inodeId 是否为本守卫所维护宿主表中的真实节点。</summary>
    Task<bool> NodeExistsAsync(string inodeId, CancellationToken ct = default);

    /// <summary>
    /// 批量解析宿主节点显示名（Id → 节点展示文本，如树节点的 Text()）。
    /// 未命中的 id 不出现在结果中；默认空实现（不参与名字解析的守卫无需覆盖）。
    /// </summary>
    Task<Dictionary<string, string>> GetNodeNamesAsync(
        IReadOnlyCollection<string> inodeIds, CancellationToken ct = default)
        => Task.FromResult(new Dictionary<string, string>());
}

/// <summary>
/// nodes 注册表守卫能力（R3）：按 host_type 判定节点类型。
/// 由 NodesRegistryGuard 实现；尚未收编进 nodes 的宿主（Line）仍走精确守卫分支。
/// </summary>
public interface INodesRegistryGuard : IInodeOwnerGuard
{
    /// <summary>nodes 中存在 Id 且 host_type 等于声明类型的行。</summary>
    Task<bool> NodeExistsOfTypeAsync(
        string nodeType, string inodeId, CancellationToken ct = default);

    /// <summary>nodes 中是否出现过该 host_type（区分「类型未认领」与「节点不存在」）。</summary>
    Task<bool> HostTypeExistsAsync(string nodeType, CancellationToken ct = default);
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

    /// <summary>
    /// 按节点类型解析守卫；无守卫认领该类型时返回 null。
    /// </summary>
    public IInodeOwnerGuard? Resolve(string nodeType)
        => _guards.FirstOrDefault(g =>
            string.Equals(g.NodeType, nodeType, StringComparison.Ordinal));

    /// <summary>
    /// 跨全部守卫批量解析 inode 显示名（Id → 展示文本）。
    /// 未被任何守卫认领的 id 不出现在结果中（调用方决定回退显示）。
    /// </summary>
    public async Task<Dictionary<string, string>> ResolveNodeNamesAsync(
        IEnumerable<string> inodeIds, CancellationToken ct = default)
    {
        var ids = inodeIds.Distinct(StringComparer.Ordinal).ToArray();
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var guard in _guards)
        {
            foreach (var (id, name) in await guard.GetNodeNamesAsync(ids, ct))
                map.TryAdd(id, name);
        }
        return map;
    }

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

    /// <summary>
    /// 按节点类型校验存在性（Edge 起点/终点）：先按 NodeType 精确解析守卫
    /// （尚未收编进 nodes 的 Line），再回退到 nodes 注册表单表匹配 host_type。
    /// </summary>
    public async Task EnsureNodeOfTypeAsync(
        string nodeType, string inodeId, CancellationToken ct = default)
    {
        var exact = Resolve(nodeType);
        if (exact is not null)
        {
            if (await exact.NodeExistsAsync(inodeId, ct)) return;
            throw NodeNotFound(nodeType, inodeId);
        }

        var typed = _guards.OfType<INodesRegistryGuard>().FirstOrDefault();
        if (typed is not null)
        {
            if (await typed.NodeExistsOfTypeAsync(nodeType, inodeId, ct)) return;
            if (!await typed.HostTypeExistsAsync(nodeType, ct))
                throw TypeUnclaimed(nodeType);
            throw NodeNotFound(nodeType, inodeId);
        }

        throw TypeUnclaimed(nodeType);
    }

    private static EavValidationException TypeUnclaimed(string nodeType)
        => new(
        [
            new ValidationError(nodeType,
                $"节点类型 '{nodeType}' 未实现 Inode 宿主，不能作为边的起点/终点。")
        ]);

    private static EavValidationException NodeNotFound(string nodeType, string inodeId)
        => new(
        [
            new ValidationError(nodeType,
                $"节点 '{inodeId}' 不存在于 {nodeType} 宿主表，无法建立该关系。")
        ]);
}

using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.EdgeSky.Services;

/// <summary>
/// 边/线路节点校验器：当 EdgeDefinition 声明了 FromNodeType/ToNodeType 时，
/// 校验对应节点在其宿主主表中真实存在，防止边指向不存在的节点（孤儿边）。
///
/// 节点必须是 Inode 宿主：通过 <see cref="InodeOwnerGuardRegistry"/> 按
/// <see cref="IInodeOwnerGuard.NodeType"/> 解析宿主守卫（各树模块注册），
/// 不再在本模块硬编码宿主表。
///
///   - nodeType 为空 → 不校验（该关系不限定节点类型，IsGlobal）；
///   - nodeType 无守卫认领（未实现 Inode）→ 拒绝（EavValidationException → 400）；
///   - nodeType 有守卫但节点不存在 → 拒绝（EavValidationException → 400）。
/// </summary>
public interface IEdgeNodeValidator
{
    /// <summary>
    /// 校验 (nodeType, nodeId) 的宿主存在性。
    /// 失败时抛出 <see cref="EavValidationException"/>（由统一异常管道映射为 400）。
    /// </summary>
    Task EnsureNodeAsync(string? nodeType, string nodeId, CancellationToken ct = default);
}

public sealed class EdgeNodeValidator : IEdgeNodeValidator
{
    private readonly InodeOwnerGuardRegistry _inodeGuards;

    public EdgeNodeValidator(InodeOwnerGuardRegistry inodeGuards)
        => _inodeGuards = inodeGuards;

    public async Task EnsureNodeAsync(
        string? nodeType, string nodeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nodeType) || string.IsNullOrWhiteSpace(nodeId))
            return;

        // 节点类型必须已实现 Inode（有宿主守卫认领），否则拒绝
        var guard = _inodeGuards.Resolve(nodeType);
        if (guard is null)
            throw new EavValidationException(
            [
                new ValidationError(nodeType,
                    $"节点类型 '{nodeType}' 未实现 Inode 宿主，不能作为边的起点/终点。")
            ]);

        if (!await guard.NodeExistsAsync(nodeId, ct))
            throw new EavValidationException(
            [
                new ValidationError(nodeType,
                    $"节点 '{nodeId}' 不存在于 {nodeType} 宿主表，无法建立该关系。")
            ]);
    }
}

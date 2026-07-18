using APromisedLand.Shared.DiberyTree.Models;

namespace APromisedLand.Shared.DiberyTree.Interfaces;

/// <summary>
/// 泛型树服务接口
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
public interface ITreeService<T>
{
    /// <summary>
    /// 获取根节点列表
    /// </summary>
    Task<IReadOnlyList<TreeNodeDto<T>>> GetRootNodesAsync(string? parentId = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取指定父节点的子节点列表（懒加载）
    /// </summary>
    Task<IReadOnlyList<TreeNodeDto<T>>> GetChildrenAsync(string parentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件查询节点
    /// </summary>
    Task<IReadOnlyList<TreeNodeDto<T>>> QueryNodesAsync(TreeQueryParams queryParams, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取完整的树（一次性加载全部）
    /// </summary>
    Task<TreeNodeDto<T>?> GetFullTreeAsync(string? rootId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建节点
    /// </summary>
    Task<TreeNodeDto<T>> CreateNodeAsync(TreeNodeDto<T> node, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新节点
    /// </summary>
    Task<TreeNodeDto<T>> UpdateNodeAsync(TreeNodeDto<T> node, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除节点（及其所有子节点）
    /// </summary>
    Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移动节点（更改父节点）
    /// </summary>
    Task<bool> MoveNodeAsync(string nodeId, string? newParentId, CancellationToken cancellationToken = default);
}
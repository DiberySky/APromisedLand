using TreeGraph.TreeSky.Models;

namespace TreeGraph.Api.TreeSky;

/// <summary>
/// 泛型树服务接口（移植自 APromisedLand.Api DiberyTree/ITreeService，与 TreeSky 组件的
/// DiberyTreeApiClient 调用面一一对应）。
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
public interface ITreeService<T>
{
    /// <summary>获取根节点列表</summary>
    Task<IReadOnlyList<TreeNodeDto<T>>> GetRootNodesAsync(string? parentId = null, CancellationToken cancellationToken = default);

    /// <summary>获取指定父节点的子节点列表（懒加载）</summary>
    Task<IReadOnlyList<TreeNodeDto<T>>> GetChildrenAsync(string parentId, CancellationToken cancellationToken = default);

    /// <summary>根据条件查询节点</summary>
    Task<IReadOnlyList<TreeNodeDto<T>>> QueryNodesAsync(TreeQueryParams queryParams, CancellationToken cancellationToken = default);

    /// <summary>获取完整的树（一次性加载全部）</summary>
    Task<TreeNodeDto<T>?> GetFullTreeAsync(string? rootId = null, CancellationToken cancellationToken = default);

    /// <summary>创建节点</summary>
    Task<TreeNodeDto<T>> CreateNodeAsync(TreeNodeDto<T> node, CancellationToken cancellationToken = default);

    /// <summary>更新节点</summary>
    Task<TreeNodeDto<T>> UpdateNodeAsync(TreeNodeDto<T> node, CancellationToken cancellationToken = default);

    /// <summary>更新指定父节点的子节点顺序</summary>
    Task<TreeNodeDto<T>> UpdateChildrenAsync(TreeNodeDto<T> nodeDto, CancellationToken cancellationToken = default);

    /// <summary>删除节点（及其所有子节点）</summary>
    Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default);

    /// <summary>移动节点（更改父节点）</summary>
    Task<bool> MoveNodeAsync(string nodeId, string? newParentId, CancellationToken cancellationToken = default);

    /// <summary>获取从根节点到指定节点的祖先路径（ID 列表，根在前）</summary>
    Task<IReadOnlyList<string>> GetAncestorPathAsync(string nodeId, CancellationToken cancellationToken = default);
}

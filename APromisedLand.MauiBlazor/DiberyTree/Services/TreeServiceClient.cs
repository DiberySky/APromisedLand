// // TreeServiceClient.cs
//
// using APromisedLand.Shared.DiberyTree.Interfaces;
// using APromisedLand.Shared.DiberyTree.Models;
//
// namespace APromisedLand.MauiBlazor.DiberyTree.Services;
//
// /// <summary>
// /// 树服务的 HTTP 实现，供 MAUI 应用调用后端 API。
// /// </summary>
// /// <typeparam name="T">节点值的类型</typeparam>
// public class TreeServiceClient<T>(DiberyTreeApiClient<T> apiClient) //: ITreeService<T>
// {
//     public Task<List<TreeNodeDto<T>>> GetRootNodesAsync(CancellationToken cancellationToken = default)
//         => apiClient.GetRootNodesAsync(cancellationToken);
//
//     public Task<List<TreeNodeDto<T>>> GetChildrenAsync(string parentId, CancellationToken cancellationToken = default)
//         => apiClient.GetChildrenAsync(parentId, cancellationToken);
//
//     public Task<List<TreeNodeDto<T>>> QueryNodesAsync(TreeQueryParams queryParams, CancellationToken cancellationToken = default)
//         => apiClient.QueryNodesAsync(queryParams, cancellationToken);
//
//     public Task<TreeNodeDto<T>?> GetFullTreeAsync(string? rootId = null, CancellationToken cancellationToken = default)
//         => apiClient.GetFullTreeAsync(rootId, cancellationToken);
//
//     public Task<TreeNodeDto<T>> CreateNodeAsync(TreeNodeDto<T> node, CancellationToken cancellationToken = default)
//         => apiClient.CreateNodeAsync(node, cancellationToken);
//
//     public Task<TreeNodeDto<T>> UpdateNodeAsync(TreeNodeDto<T> node, CancellationToken cancellationToken = default)
//         => apiClient.UpdateNodeAsync(node, cancellationToken);
//
//     public Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default)
//         => apiClient.DeleteNodeAsync(nodeId, cancellationToken);
//
//     public Task<bool> MoveNodeAsync(string nodeId, string? newParentId, CancellationToken cancellationToken = default)
//         => apiClient.MoveNodeAsync(nodeId, newParentId, cancellationToken);
// }
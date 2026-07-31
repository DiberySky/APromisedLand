// DiberyTreeApiClient.cs

using System.Net.Http.Json;
using APromisedLand.Shared.DiberyTree.Models;

namespace APromisedLand.MauiBlazor.DiberyTree.Services;

/// <summary>
/// 泛型树 API 客户端，用于调用后端的树控制器（TreeControllerBase<T>）。
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
public class DiberyTreeApiClient<T>(HttpClient httpClient)
{
    private readonly string _basePath = typeof(T).Name;

    // 根据泛型类型名称自动构建控制器路径，例如 CategoryTree => "api/CategoryTree"

    /// <summary>
    /// 获取所有根节点
    /// </summary>
    public async Task<IReadOnlyList<TreeNodeDto<T>>> GetRootNodesAsync(string? parentId = null,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        if (parentId != null)
        {
            response = await httpClient.GetAsync($"{_basePath}/roots/{Uri.EscapeDataString(parentId)}",
                cancellationToken);
        }
        else
        {
            response = await httpClient.GetAsync($"{_basePath}/roots", cancellationToken);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<TreeNodeDto<T>>>(
                   cancellationToken: cancellationToken)
               ?? new List<TreeNodeDto<T>>();
    }

    /// <summary>
    /// 获取指定父节点的子节点（懒加载）
    /// </summary>
    public async Task<IReadOnlyList<TreeNodeDto<T>>> GetChildrenAsync(string parentId,
        CancellationToken cancellationToken = default)
    {
        var response =
            await httpClient.GetAsync($"{_basePath}/children/{Uri.EscapeDataString(parentId)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<TreeNodeDto<T>>>(
                   cancellationToken: cancellationToken)
               ?? new List<TreeNodeDto<T>>();
    }

    /// <summary>
    /// 获取从根节点到指定节点的祖先路径
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAncestorPathAsync(string nodeId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"{_basePath}/{Uri.EscapeDataString(nodeId)}/ancestors", 
            cancellationToken);
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<string>>(
                   cancellationToken: cancellationToken)
               ?? new List<string>();
    }
    
    /// <summary>
    /// 条件查询节点（分页、搜索、过滤）
    /// </summary>
    public async Task<IReadOnlyList<TreeNodeDto<T>>> QueryNodesAsync(TreeQueryParams queryParams,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{_basePath}/query", queryParams, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<TreeNodeDto<T>>>(
                   cancellationToken: cancellationToken)
               ?? new List<TreeNodeDto<T>>();
    }

    /// <summary>
    /// 获取完整树（包含所有后代）
    /// </summary>
    public async Task<TreeNodeDto<T>?> GetFullTreeAsync(string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_basePath}/full";
        if (!string.IsNullOrEmpty(rootId))
            url += $"?rootId={Uri.EscapeDataString(rootId)}";

        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TreeNodeDto<T>>(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 创建新节点
    /// </summary>
    public async Task<TreeNodeDto<T>> CreateNodeAsync(TreeNodeDto<T> node,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(_basePath, node, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TreeNodeDto<T>>(cancellationToken: cancellationToken))!;
    }

    /// <summary>
    /// 更新节点信息（不包括 ParentId，请使用 Move 方法移动）
    /// </summary>
    public async Task<TreeNodeDto<T>> UpdateChildrenAsync(TreeNodeDto<T>  nodeDto,
        CancellationToken cancellationToken = default)
    {
        // var url = $"{_basePath}/move?nodeId={Uri.EscapeDataString(nodeId)}";

        var response =
            await httpClient.PostAsJsonAsync($"{_basePath}/children", nodeDto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TreeNodeDto<T>>(cancellationToken: cancellationToken))!;
    }

    /// <summary>
    /// 更新节点信息（不包括 ParentId，请使用 Move 方法移动）
    /// </summary>
    public async Task<TreeNodeDto<T>> UpdateNodeAsync(string id, TreeNodeDto<T> node,
        CancellationToken cancellationToken = default)
    {
        var response =
            await httpClient.PutAsJsonAsync($"{_basePath}/{Uri.EscapeDataString(id)}", node, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TreeNodeDto<T>>(cancellationToken: cancellationToken))!;
    }

    /// <summary>
    /// 删除节点及其所有子节点
    /// </summary>
    public async Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{_basePath}/{Uri.EscapeDataString(nodeId)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 移动节点到新的父节点（null 表示移至根）
    /// </summary>
    public async Task<bool> MoveNodeAsync(string nodeId, string? newParentId,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_basePath}/move?nodeId={Uri.EscapeDataString(nodeId)}";
        if (!string.IsNullOrEmpty(newParentId))
            url += $"&newParentId={Uri.EscapeDataString(newParentId)}";

        var response = await httpClient.PostAsync(url, null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
    }
}
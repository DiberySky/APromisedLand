// DiberyTreeApiClient.cs

using System.Net.Http.Json;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.DTOs;
using MudBlazor;

namespace APromisedLand.MauiBlazor.DiberyTree.Services;

/// <summary>
/// 专门用于调用 DiberyTree 服务的 HTTP 客户端。
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
public class DiberyTreeApiClient<T>(HttpClient httpClient)
{
    private readonly string _basePath = typeof(T).Name + "Tree";

    public async Task<List<TreeNodeDto<T>>> GetRootNodesAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{_basePath}/roots", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TreeNodeDto<T>>>(cancellationToken: cancellationToken)
               ?? new List<TreeNodeDto<T>>();
    }

    public async Task<List<TreeNodeDto<T>>> GetChildrenAsync(string parentId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{_basePath}/children/{parentId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TreeNodeDto<T>>>(cancellationToken: cancellationToken)
               ?? new List<TreeNodeDto<T>>();
    }
    
    public async Task<List<TreeNodeDto<T>>> QueryNodesAsync(TreeQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{_basePath}/query", queryParams, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TreeNodeDto<T>>>(cancellationToken: cancellationToken)
               ?? new List<TreeNodeDto<T>>();
    }
    
    public async Task<TreeNodeDto<T>?> GetFullTreeAsync(string? rootId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{_basePath}/full";
        if (!string.IsNullOrEmpty(rootId))
            url += $"?rootId={Uri.EscapeDataString(rootId)}";
    
        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TreeNodeDto<T>>(cancellationToken: cancellationToken);
    }
    
    public async Task<TreeNodeDto<T>> CreateNodeAsync(TreeNodeDto<T> node, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(_basePath, node, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TreeNodeDto<T>>(cancellationToken: cancellationToken))!;
    }
    
    public async Task<TreeNodeDto<T>> UpdateNodeAsync(TreeNodeDto<T> node, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{_basePath}/{node.Id}", node, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TreeNodeDto<T>>(cancellationToken: cancellationToken))!;
    }
    
    public async Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{_basePath}/{nodeId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
    }
    
    public async Task<bool> MoveNodeAsync(string nodeId, string? newParentId, CancellationToken cancellationToken = default)
    {
        var url = $"{_basePath}/move?nodeId={Uri.EscapeDataString(nodeId)}";
        if (!string.IsNullOrEmpty(newParentId))
            url += $"&newParentId={Uri.EscapeDataString(newParentId)}";
        var response = await httpClient.PostAsync(url, null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
    }
}
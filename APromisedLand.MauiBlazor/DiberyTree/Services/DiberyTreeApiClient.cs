// DiberyTreeApiClient.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DTOs;

namespace APromisedLand.MauiBlazor.DiberyTree.Services;

/// <summary>
/// 泛型树 API 客户端，用于调用后端的 TreeControllerBase<T>，
/// 包含树节点 CRUD 以及节点属性值的操作（属性定义已分离至 AttributeApiClient，表数据见 TableValueApiClient）。
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
public class DiberyTreeApiClient<T>(HttpClient httpClient)
{
    private readonly string _basePath = typeof(T).Name; // 例如 "CategoryTree"

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 发送请求并从标准 ApiResponse 中提取 Data，若失败则抛出异常。
    /// </summary>
    private async Task<TData> SendAndGetDataAsync<TData>(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.SendAsync(request, cancellationToken);
        
        await EnsureSuccessWithApiResponseAsync(response);

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<TData>>(cancellationToken);
        if (apiResponse?.Success != true)
            throw new Exception(apiResponse?.Message ?? "操作失败，未返回具体错误信息");
        
        return apiResponse.Data!;
    }

    /// <summary>
    /// 针对可能返回 404 的方法，返回 null 而不抛出异常。
    /// </summary>
    private async Task<TData?> SendAndGetDataOrNullAsync<TData>(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
        where TData : class
    {
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<TData>>(cancellationToken);
        return apiResponse?.Success == true ? apiResponse.Data : null;
    }

    /// <summary>
    /// 检查状态码，若失败则尝试从响应中读取 ApiResponse 消息并抛出。
    /// </summary>
    private static async Task EnsureSuccessWithApiResponseAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        // 尝试读取标准 ApiResponse 中的错误信息
        // try
        // {
            var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.Message))
                throw new HttpRequestException($"请求失败: {errorResponse.Message}", null, response.StatusCode);
        // }
        // catch (Exception ex)
        // {
        //     // 若无法解析，则回退到默认 EnsureSuccessStatusCode
        //     // response.EnsureSuccessStatusCode();
        // }
    }

    // ==================== 树节点操作 ====================

    /// <summary>
    /// 获取所有根节点；若指定 rootId，则获取该特定根节点（对应 roots/{rootId} 路由）。
    /// </summary>
    public async Task<IReadOnlyList<TreeNodeDto<T>>> GetRootNodesAsync(
        string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        var url = string.IsNullOrEmpty(rootId)
            ? $"{_basePath}/roots"
            : $"{_basePath}/roots/{Uri.EscapeDataString(rootId)}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendAndGetDataAsync<IReadOnlyList<TreeNodeDto<T>>>(request, cancellationToken);
    }

    /// <summary>
    /// 获取指定父节点的子节点（懒加载）
    /// </summary>
    public async Task<IReadOnlyList<TreeNodeDto<T>>> GetChildrenAsync(
        string parentId,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_basePath}/children/{Uri.EscapeDataString(parentId)}");
        return await SendAndGetDataAsync<IReadOnlyList<TreeNodeDto<T>>>(request, cancellationToken);
    }

    /// <summary>
    /// 获取从根节点到指定节点的祖先路径
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAncestorPathAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_basePath}/{Uri.EscapeDataString(nodeId)}/ancestors");
        return await SendAndGetDataAsync<IReadOnlyList<string>>(request, cancellationToken);
    }

    /// <summary>
    /// 条件查询节点（分页、搜索、过滤）
    /// </summary>
    public async Task<IReadOnlyList<TreeNodeDto<T>>> QueryNodesAsync(
        TreeQueryParams queryParams,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_basePath}/query")
        {
            Content = JsonContent.Create(queryParams)
        };
        return await SendAndGetDataAsync<IReadOnlyList<TreeNodeDto<T>>>(request, cancellationToken);
    }

    /// <summary>
    /// 获取完整树（包含所有后代）；若指定 rootId，则从该节点开始展开。
    /// </summary>
    public async Task<TreeNodeDto<T>?> GetFullTreeAsync(
        string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_basePath}/full";
        if (!string.IsNullOrEmpty(rootId))
            url += $"?rootId={Uri.EscapeDataString(rootId)}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendAndGetDataOrNullAsync<TreeNodeDto<T>>(request, cancellationToken);
    }

    /// <summary>
    /// 创建新节点
    /// </summary>
    public async Task<TreeNodeDto<T>> CreateNodeAsync(
        TreeNodeDto<T> node,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _basePath)
        {
            Content = JsonContent.Create(node)
        };
        return await SendAndGetDataAsync<TreeNodeDto<T>>(request, cancellationToken);
    }

    /// <summary>
    /// 更新节点的子项顺序（Reorder）
    /// </summary>
    public async Task<TreeNodeDto<T>> UpdateChildrenAsync(
        TreeNodeDto<T> nodeDto,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_basePath}/children")
        {
            Content = JsonContent.Create(nodeDto)
        };
        return await SendAndGetDataAsync<TreeNodeDto<T>>(request, cancellationToken);
    }

    /// <summary>
    /// 更新节点信息（不包括 ParentId，请使用 Move 方法移动）
    /// </summary>
    public async Task<TreeNodeDto<T>> UpdateNodeAsync(
        string id,
        TreeNodeDto<T> node,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"{_basePath}/{Uri.EscapeDataString(id)}")
        {
            Content = JsonContent.Create(node)
        };
        return await SendAndGetDataAsync<TreeNodeDto<T>>(request, cancellationToken);
    }

    /// <summary>
    /// 删除节点及其所有子节点（不存在返回 false，其他失败抛异常）
    /// </summary>
    public async Task<bool> DeleteNodeAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"{_basePath}/{Uri.EscapeDataString(nodeId)}");
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(cancellationToken);
        return apiResponse?.Success == true && apiResponse.Data;
    }

    /// <summary>
    /// 移动节点到新的父节点（null 表示移至根）。若节点或新父节点不存在返回 false，其他失败抛异常。
    /// </summary>
    public async Task<bool> MoveNodeAsync(
        string nodeId,
        string? newParentId,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_basePath}/move?nodeId={Uri.EscapeDataString(nodeId)}";
        if (!string.IsNullOrEmpty(newParentId))
            url += $"&newParentId={Uri.EscapeDataString(newParentId)}";
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(cancellationToken);
        return apiResponse?.Success == true && apiResponse.Data;
    }

    // 属性定义操作已分离至 AttributeApiClient（属性定义为全局资源，不耦合具体树）
    
    // // ==================== 属性值操作 ====================
    //
    // /// <summary>
    // /// 为指定节点添加一个属性值（201 No Content）
    // /// </summary>
    // public async Task AddValueAsync(
    //     string nodeId,
    //     AddValueDto dto,
    //     CancellationToken cancellationToken = default)
    // {
    //     var request = new HttpRequestMessage(HttpMethod.Post, $"{_basePath}/{Uri.EscapeDataString(nodeId)}/attributes/values")
    //     {
    //         Content = JsonContent.Create(dto)
    //     };
    //     var response = await httpClient.SendAsync(request, cancellationToken);
    //     await EnsureSuccessWithApiResponseAsync(response);
    //     // 201 无内容，无需解析
    // }
    //
    // /// <summary>
    // /// 获取指定节点的单个属性值（不存在返回 null）
    // /// </summary>
    // public async Task<AttributeJsonValueDto?> GetSingleValueAsync(
    //     string nodeId,
    //     string valueId,
    //     CancellationToken cancellationToken = default)
    // {
    //     var request = new HttpRequestMessage(HttpMethod.Get,
    //         $"{_basePath}/{Uri.EscapeDataString(nodeId)}/attributes/values/{Uri.EscapeDataString(valueId)}");
    //     return await SendAndGetDataOrNullAsync<AttributeJsonValueDto>(request, cancellationToken);
    // }
    //
    // /// <summary>
    // /// 获取指定节点的所有属性值（返回 NodeAttributesDto 包含属性列表，不存在返回 null）
    // /// </summary>
    // public async Task<NodeAttributesDto?> GetAllValuesAsync(
    //     string nodeId,
    //     CancellationToken cancellationToken = default)
    // {
    //     var request = new HttpRequestMessage(HttpMethod.Get,
    //         $"{_basePath}/{Uri.EscapeDataString(nodeId)}/attributes/values");
    //     return await SendAndGetDataOrNullAsync<NodeAttributesDto>(request, cancellationToken);
    // }
    //
    // /// <summary>
    // /// 更新指定节点的某个属性值（204 No Content）
    // /// </summary>
    // public async Task UpdateValueAsync(
    //     string nodeId,
    //     string valueId,
    //     UpdateValueDto valueDto,
    //     CancellationToken cancellationToken = default)
    // {
    //     var dto = new { Value = valueDto };
    //     var request = new HttpRequestMessage(HttpMethod.Put,
    //         $"{_basePath}/{Uri.EscapeDataString(nodeId)}/attributes/values/{Uri.EscapeDataString(valueId)}")
    //     {
    //         Content = JsonContent.Create(dto)
    //     };
    //     var response = await httpClient.SendAsync(request, cancellationToken);
    //     await EnsureSuccessWithApiResponseAsync(response);
    // }
    //
    // /// <summary>
    // /// 删除指定节点的某个属性值（不存在返回 false，其他失败抛异常）
    // /// </summary>
    // public async Task<bool> DeleteValueAsync(
    //     string nodeId,
    //     string valueId,
    //     CancellationToken cancellationToken = default)
    // {
    //     var request = new HttpRequestMessage(HttpMethod.Delete,
    //         $"{_basePath}/{Uri.EscapeDataString(nodeId)}/attributes/values/{Uri.EscapeDataString(valueId)}");
    //     var response = await httpClient.SendAsync(request, cancellationToken);
    //     if (response.StatusCode == HttpStatusCode.NotFound)
    //         return false;
    //     await EnsureSuccessWithApiResponseAsync(response);
    //     var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(cancellationToken);
    //     return apiResponse?.Success == true && apiResponse.Data;
    // }
}
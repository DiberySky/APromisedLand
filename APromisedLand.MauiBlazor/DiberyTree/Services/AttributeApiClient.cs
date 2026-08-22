// AttributeApiClient.cs
using System.Net;
using System.Net.Http.Json;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DTOs;

namespace APromisedLand.MauiBlazor.DiberyTree.Services;

/// <summary>
/// 属性定义 API 客户端，调用后端的 <see cref="APromisedLand.Api.Projects.DiberyTree.AttributeControllerBase"/>
/// （派生 AttributesController → 路由 attributes/definitions）。
/// <para>属性定义为全局资源，不耦合具体树，故无 _basePath 前缀。</para>
/// </summary>
public class AttributeApiClient(HttpClient httpClient)
{
    private const string BasePath = "attributes";

    // ---------- 辅助 ----------

    private async Task<TData> SendAndGetDataAsync<TData>(
        HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<TData>>(cancellationToken);
        if (apiResponse?.Success != true)
            throw new Exception(apiResponse?.Message ?? "操作失败，未返回具体错误信息");
        return apiResponse.Data!;
    }

    private async Task<TData?> SendAndGetDataOrNullAsync<TData>(
        HttpRequestMessage request, CancellationToken cancellationToken = default)
        where TData : class
    {
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<TData>>(cancellationToken);
        return apiResponse?.Success == true ? apiResponse.Data : null;
    }

    private static async Task EnsureSuccessWithApiResponseAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;
        try
        {
            var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.Message))
                throw new HttpRequestException($"请求失败: {errorResponse.Message}", null, response.StatusCode);
        }
        catch
        {
            response.EnsureSuccessStatusCode();
        }
    }

    // ---------- 属性定义 ----------

    /// <summary>获取所有属性定义</summary>
    public async Task<IReadOnlyList<AttributeDefinitionDto>> GetAllDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{BasePath}/definitions");
        return await SendAndGetDataAsync<IReadOnlyList<AttributeDefinitionDto>>(request, cancellationToken);
    }

    // /// <summary>获取所有属性类型</summary>
    // public async Task<IReadOnlyList<AttributeType>> GetAttributeTypesAsync(
    //     CancellationToken cancellationToken = default)
    // {
    //     var request = new HttpRequestMessage(HttpMethod.Get, $"{BasePath}/types");
    //     return await SendAndGetDataAsync<IReadOnlyList<AttributeType>>(request, cancellationToken);
    // }

    /// <summary>根据 ID 获取属性定义（不存在返回 null）</summary>
    public async Task<AttributeDefinitionDto?> GetDefinitionByIdAsync(
        string id, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BasePath}/definitions/{Uri.EscapeDataString(id)}");
        return await SendAndGetDataOrNullAsync<AttributeDefinitionDto>(request, cancellationToken);
    }

    /// <summary>创建新的属性定义</summary>
    public async Task<AttributeDefinitionDto> CreateDefinitionAsync(
        AttributeDefinitionCreateDto dto, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BasePath}/definitions")
        {
            Content = JsonContent.Create(dto)
        };
        return await SendAndGetDataAsync<AttributeDefinitionDto>(request, cancellationToken);
    }

    /// <summary>更新属性定义</summary>
    public async Task<AttributeDefinitionDto> UpdateDefinitionAsync(
        string id, AttributeDefinitionUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"{BasePath}/definitions/{Uri.EscapeDataString(id)}")
        {
            Content = JsonContent.Create(dto)
        };
        return await SendAndGetDataAsync<AttributeDefinitionDto>(request, cancellationToken);
    }

    /// <summary>删除属性定义（成功返回 true，不存在返回 false）</summary>
    public async Task<bool> DeleteDefinitionAsync(
        string id, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{BasePath}/definitions/{Uri.EscapeDataString(id)}");
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(cancellationToken);
        return apiResponse?.Success == true && apiResponse.Data;
    }

    // ---------- 动态表：表定义 & 列定义 ----------

    /// <summary>获取所有「表格」类型的表定义。</summary>
    public async Task<IReadOnlyList<AttributeDefinitionDto>> ListTablesAsync(
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{BasePath}/tables");
        return await SendAndGetDataAsync<IReadOnlyList<AttributeDefinitionDto>>(request, cancellationToken);
    }

    /// <summary>获取指定表下的所有列定义（按 Order 排序）。</summary>
    public async Task<IReadOnlyList<AttributeDefinitionDto>> ListTableColumnsAsync(
        string tableId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BasePath}/tables/{Uri.EscapeDataString(tableId)}/columns");
        var resp = await httpClient.SendAsync(request, cancellationToken);
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return Array.Empty<AttributeDefinitionDto>();
        await EnsureSuccessWithApiResponseAsync(resp);
        var apiResponse = await resp.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<AttributeDefinitionDto>>>(cancellationToken);
        return apiResponse?.Success == true ? apiResponse.Data! : Array.Empty<AttributeDefinitionDto>();
    }

    /// <summary>在指定表下新建列定义（ParentId 由服务端强制为 tableId）。</summary>
    public async Task<AttributeDefinitionDto> CreateTableColumnAsync(
        string tableId, AttributeDefinitionCreateDto dto, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{BasePath}/tables/{Uri.EscapeDataString(tableId)}/columns")
        {
            Content = JsonContent.Create(dto)
        };
        return await SendAndGetDataAsync<AttributeDefinitionDto>(request, cancellationToken);
    }
}

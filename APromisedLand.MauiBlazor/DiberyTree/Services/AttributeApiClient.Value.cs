using System.Net;
using System.Net.Http.Json;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DTOs;

namespace APromisedLand.MauiBlazor.DiberyTree.Services;

public partial class AttributeApiClient
{
    // ==================== 属性值操作 ====================
    
    /// <summary>
    /// 为指定节点添加一个属性值（201 No Content）
    /// </summary>
    public async Task AddValueAsync(
        string nodeId,
        AddValueDto dto,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BasePath}/{Uri.EscapeDataString(nodeId)}/attributes/values")
        {
            Content = JsonContent.Create(dto)
        };
        var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessWithApiResponseAsync(response);
        // 201 无内容，无需解析
    }
    
    /// <summary>
    /// 获取指定节点的单个属性值（不存在返回 null）
    /// </summary>
    public async Task<AttributeJsonValueDto?> GetSingleValueAsync(
        string nodeId,
        string valueId,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BasePath}/{Uri.EscapeDataString(nodeId)}/attributes/values/{Uri.EscapeDataString(valueId)}");
        return await SendAndGetDataOrNullAsync<AttributeJsonValueDto>(request, cancellationToken);
    }
    
    /// <summary>
    /// 获取指定节点的所有属性值（返回 NodeAttributesDto 包含属性列表，不存在返回 null）
    /// </summary>
    public async Task<NodeAttributesDto?> GetAllValuesAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BasePath}/{Uri.EscapeDataString(nodeId)}/attributes/values");
        return await SendAndGetDataOrNullAsync<NodeAttributesDto>(request, cancellationToken);
    }
    
    /// <summary>
    /// 更新指定节点的某个属性值（204 No Content）
    /// </summary>
    public async Task UpdateValueAsync(
        string nodeId,
        string valueId,
        UpdateValueDto valueDto,
        CancellationToken cancellationToken = default)
    {
        var dto = new { Value = valueDto };
        
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"{BasePath}/{Uri.EscapeDataString(nodeId)}/attributes/values/{Uri.EscapeDataString(valueId)}")
            {
                Content = JsonContent.Create(dto)
            };
        
        var response = await httpClient.SendAsync(request, cancellationToken);
        
        await EnsureSuccessWithApiResponseAsync(response);
    }
    
    /// <summary>
    /// 删除指定节点的某个属性值（不存在返回 false，其他失败抛异常）
    /// </summary>
    public async Task<bool> DeleteValueAsync(
        string nodeId,
        string valueId,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{BasePath}/{Uri.EscapeDataString(nodeId)}/attributes/values/{Uri.EscapeDataString(valueId)}");
        
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        
        await EnsureSuccessWithApiResponseAsync(response);
        
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(cancellationToken);
        return apiResponse?.Success == true && apiResponse.Data;
    }

}
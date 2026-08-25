using System.Net;
using System.Net.Http.Json;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DTOs;

namespace APromisedLand.MauiBlazor.DiberyTree.Services;

public partial class AttributeApiClient
{
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
}
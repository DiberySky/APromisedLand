using System.Net;
using System.Net.Http.Json;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DTOs;

namespace APromisedLand.MauiBlazor.DiberyTree.Services;

public partial class AttributeApiClient
{
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
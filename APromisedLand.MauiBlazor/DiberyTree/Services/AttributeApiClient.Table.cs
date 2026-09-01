using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DTOs;

namespace APromisedLand.MauiBlazor.DiberyTree.Services;

public partial class AttributeApiClient
{
    // ---------- 动态表：表定义 & 列定义 ----------

    /// <summary>获取所有「表格」类型的表定义。</summary>
    public async Task<IReadOnlyList<AttributeDefinition>> ListTablesAsync(
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{BasePath}/tables");
        return await SendAndGetDataAsync<IReadOnlyList<AttributeDefinition>>(request, cancellationToken);
    }

    /// <summary>获取指定表下的所有列定义（按 Order 排序）。</summary>
    public async Task<IReadOnlyList<AttributeDefinition>> ListTableColumnsAsync(
        string tableId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BasePath}/tables/{Uri.EscapeDataString(tableId)}/columns");
        
        var resp = await httpClient.SendAsync(request, cancellationToken);
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return Array.Empty<AttributeDefinition>();
        
        await EnsureSuccessWithApiResponseAsync(resp);
        
        var apiResponse = await resp.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<AttributeDefinition>>>(cancellationToken);
        return apiResponse?.Success == true ? apiResponse.Data! : Array.Empty<AttributeDefinition>();
    }

    /// <summary>在指定表下新建列定义（ParentId 由服务端强制为 tableId）。</summary>
    public async Task<AttributeDefinition> CreateTableColumnAsync(
        string tableId, AttributeDefinitionCreateDto dto, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{BasePath}/tables/{Uri.EscapeDataString(tableId)}/columns")
            {
                Content = JsonContent.Create(dto)
            };
        
        return await SendAndGetDataAsync<AttributeDefinition>(request, cancellationToken);
    }
    
    // ---------- 表行数据 ----------

    /// <summary>向指定表添加一行数据（列定义 Id → JSON 值），返回该行 DTO</summary>
    public async Task<TableRowDto> AddRowAsync(AddTableRowDto addDto, 
        CancellationToken cancellationToken = default)
    {
        // var dto = new AddTableRowDto { Values = values };
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{BasePath}/tables/{Uri.EscapeDataString(addDto.TableId)}/rows")
        {
            Content = JsonContent.Create(addDto)
        };
        return await SendAndGetDataAsync<TableRowDto>(request, cancellationToken);
    }

    /// <summary>列出指定表的所有行实例（含各列值），按 RowNo 排序</summary>
    public async Task<List<TableRowDto>> ListRowsAsync(
        string tableId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BasePath}/tables/{Uri.EscapeDataString(tableId)}/rows");
        return await SendAndGetDataAsync<List<TableRowDto>>(request, cancellationToken);
    }

    /// <summary>获取单行实例（含各列值），不存在返回 null</summary>
    public async Task<TableRowDto?> GetRowAsync(
        string rowId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BasePath}/tables/rows/{Uri.EscapeDataString(rowId)}");
        return await SendAndGetDataOrNullAsync<TableRowDto>(request, cancellationToken);
    }

    /// <summary>更新某行的列值（先删旧列值再重建），返回该行 DTO</summary>
    public async Task<TableRowDto> UpdateRowAsync(
        UpdateTableRowDto updateDto, 
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"{BasePath}/tables/rows/{Uri.EscapeDataString(updateDto.RowId)}")
        {
            Content = JsonContent.Create(updateDto)
        };
        return await SendAndGetDataAsync<TableRowDto>(request, cancellationToken);
    }

    /// <summary>删除行实例及其所有列值（成功返回 true，不存在返回 false）</summary>
    public async Task<bool> DeleteRowAsync(
        string rowId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{BasePath}/tables/rows/{Uri.EscapeDataString(rowId)}");
        
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        
        await EnsureSuccessWithApiResponseAsync(response);

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<string>>(cancellationToken);
        return apiResponse!.Success;
    }
}
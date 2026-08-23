// TableValueApiClient.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DTOs;

namespace APromisedLand.MauiBlazor.DiberyTree.Services;

/// <summary>
/// 动态表数据 API 客户端，调用后端的 <see cref="APromisedLand.Api.Projects.DiberyTree.TableValueControllerBase"/>
/// （派生 TableValuesController → 路由 tablevalues/...）。
/// 负责行实例与列值的 CRUD。
/// </summary>
public class TableValueApiClient(HttpClient httpClient)
{
    private const string BasePath = "AttributeTableValue";

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

    // ---------- 表行数据 ----------

    /// <summary>向指定表添加一行数据（列定义 Id → JSON 值），返回该行 DTO</summary>
    public async Task<TableRowDto> AddRowAsync(string nodeId, string tabledId, 
        string tableDefId, Dictionary<string, JsonElement> values, CancellationToken cancellationToken = default)
    {
        var dto = new AddTableRowDto { Values = values };
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{BasePath}/node/{Uri.EscapeDataString(nodeId)}/table/{Uri.EscapeDataString(tabledId)}/definition/{Uri.EscapeDataString(tableDefId)}/rows")
        {
            Content = JsonContent.Create(dto)
        };
        return await SendAndGetDataAsync<TableRowDto>(request, cancellationToken);
    }

    /// <summary>列出指定表的所有行实例（含各列值），按 RowNo 排序</summary>
    public async Task<List<TableRowDto>> ListRowsAsync(
        string tableId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BasePath}/{Uri.EscapeDataString(tableId)}/rows");
        return await SendAndGetDataAsync<List<TableRowDto>>(request, cancellationToken);
    }

    /// <summary>获取单行实例（含各列值），不存在返回 null</summary>
    public async Task<TableRowDto?> GetRowAsync(
        string rowId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BasePath}/rows/{Uri.EscapeDataString(rowId)}");
        return await SendAndGetDataOrNullAsync<TableRowDto>(request, cancellationToken);
    }

    /// <summary>更新某行的列值（先删旧列值再重建），返回该行 DTO</summary>
    public async Task<TableRowDto> UpdateRowAsync(string tabledId, 
        string rowId, Dictionary<string, JsonElement> values, CancellationToken cancellationToken = default)
    {
        var dto = new UpdateTableRowDto { Values = values };
        var request = new HttpRequestMessage(HttpMethod.Put,
            $"{BasePath}/rows/{Uri.EscapeDataString(rowId)}")
        {
            Content = JsonContent.Create(dto)
        };
        return await SendAndGetDataAsync<TableRowDto>(request, cancellationToken);
    }

    /// <summary>删除行实例及其所有列值（成功返回 true，不存在返回 false）</summary>
    public async Task<bool> DeleteRowAsync(
        string rowId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{BasePath}/rows/{Uri.EscapeDataString(rowId)}");
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(cancellationToken);
        return apiResponse?.Success == true && apiResponse.Data;
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Blazor.Shared.LineSky;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Blazor.Shared.LineSky.Services;

/// <summary>
/// 线路节点（LineNode）实体 CRUD HTTP 客户端，对齐后端 LineNodeController（路由 api/LineNode）。
/// 响应为 { success, message, data } 信封，本类负责解包。
/// </summary>
public class LineNodeManageApiClient(HttpClient httpClient)
{
    private const string BasePath = "api/LineNode";

    // GET api/LineNode?lineId={lineId}
    public async Task<IReadOnlyList<LineNodeDto>> GetByLineAsync(
        string lineId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"{BasePath}?lineId={Uri.EscapeDataString(lineId)}", cancellationToken);
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<LineNodeDto>>>(cancellationToken);
        return apiResponse?.Success != true || apiResponse.Data == null ? [] : apiResponse.Data;
    }

    // GET api/LineNode/{id}；不存在返回 null
    public async Task<LineNodeDto?> GetByIdAsync(
        string id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"{BasePath}/{Uri.EscapeDataString(id)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<LineNodeDto>>(cancellationToken);
        return apiResponse?.Success == true ? apiResponse.Data : null;
    }

    // POST api/LineNode
    public async Task<LineNodeDto> CreateAsync(LineNodeDto node, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BasePath, node, cancellationToken);
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<LineNodeDto>>(cancellationToken);
        if (apiResponse?.Success != true || apiResponse.Data is null)
            throw new Exception(apiResponse?.Message ?? "创建失败，未返回具体错误信息");
        return apiResponse.Data;
    }

    // PUT api/LineNode/{id}；不存在返回 null
    public async Task<LineNodeDto?> UpdateAsync(
        string id, LineNodeDto node, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"{BasePath}/{Uri.EscapeDataString(id)}", node, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<LineNodeDto>>(cancellationToken);
        if (apiResponse?.Success != true)
            throw new Exception(apiResponse?.Message ?? "更新失败，未返回具体错误信息");
        return apiResponse.Data;
    }

    // DELETE api/LineNode/{id}；不存在返回 false
    // 后端返回 204 NoContent（无响应体），故仅以状态码判定成功。
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{BasePath}/{Uri.EscapeDataString(id)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        await EnsureSuccessWithApiResponseAsync(response);
        return true;
    }

    /// <summary>
    /// 检查状态码，若失败则抛出携带真实原因（ApiResponse{message} 或 {error}）的异常。
    /// </summary>
    private static async Task EnsureSuccessWithApiResponseAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        string? message = null;
        try
        {
            var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (doc.ValueKind == JsonValueKind.Object)
            {
                string? Get(string name) =>
                    doc.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                        ? v.GetString()
                        : null;

                message = Get("error") ?? Get("Error") ?? Get("message") ?? Get("Message");
            }
        }
        catch (JsonException) { }

        throw new HttpRequestException(
            message is null
                ? $"请求失败: {(int)response.StatusCode} {response.ReasonPhrase}"
                : $"请求失败: {message}",
            null, response.StatusCode);
    }
}

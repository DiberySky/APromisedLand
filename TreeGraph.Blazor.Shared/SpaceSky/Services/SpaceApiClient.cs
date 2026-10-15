using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Blazor.Shared.SpaceSky.Services;

/// <summary>
/// 空间（Space）CRUD HTTP 客户端，对齐后端 SpaceController（路由 /Space）。
/// 响应为 { success, message, data } 信封，本类负责解包。
///
/// 用法：Program.cs 中注册（与树 Api 同款 HttpClient，BaseAddress 指向 TreeGraph.Api）
///   services.AddScoped(sp => new SpaceApiClient(sp.GetRequiredService&lt;HttpClient&gt;()));
/// </summary>
public class SpaceApiClient(HttpClient httpClient)
{
    private const string BasePath = "Space";

    // GET /Space
    public async Task<IReadOnlyList<SpaceDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(BasePath, cancellationToken);
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<SpaceDto>>>(cancellationToken);
        return apiResponse?.Success == true && apiResponse.Data != null
            ? apiResponse.Data
            : [];
    }

    // GET /Space/{id}；不存在返回 null
    public async Task<SpaceDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BasePath}/{Uri.EscapeDataString(id)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<SpaceDto>>(cancellationToken);
        return apiResponse?.Success == true ? apiResponse.Data : null;
    }

    // POST /Space
    public async Task<SpaceDto> CreateAsync(SpaceDto space, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BasePath, space, cancellationToken);
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<SpaceDto>>(cancellationToken);
        if (apiResponse?.Success != true)
            throw new Exception(apiResponse?.Message ?? "创建失败，未返回具体错误信息");
        return apiResponse.Data!;
    }

    // PUT /Space/{id}；不存在返回 null
    public async Task<SpaceDto?> UpdateAsync(string id, SpaceDto space, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{BasePath}/{Uri.EscapeDataString(id)}", space, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<SpaceDto>>(cancellationToken);
        if (apiResponse?.Success != true)
            throw new Exception(apiResponse?.Message ?? "更新失败，未返回具体错误信息");
        return apiResponse.Data!;
    }

    // DELETE /Space/{id}；不存在返回 false
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{BasePath}/{Uri.EscapeDataString(id)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        await EnsureSuccessWithApiResponseAsync(response);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(cancellationToken);
        return apiResponse?.Success == true && apiResponse.Data;
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

using System.Net.Http.Json;
using TreeGraph.Shared.StringTreeSky.Contracts;

namespace TreeGraph.Blazor.Shared.StringTreeSky.Services;

/// <summary>
/// 空间 HTTP 客户端。走 api/string-tree/spaces。
/// </summary>
public class SpaceApiClient : ISpaceClient
{
    private readonly HttpClient _http;
    private readonly string _basePath;

    public SpaceApiClient(HttpClient http, StringTreeSkyOptions options)
    {
        _http = http;
        // options.BasePath 默认 "api/string-tree"，拼成 "api/string-tree/spaces"
        _basePath = $"{options.BasePath.TrimEnd('/')}/spaces";
    }

    public async Task<List<SpaceDto>> ListSpacesAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetFromJsonAsync<StringTreeResponse<List<SpaceDto>>>(_basePath, ct);
        return resp?.Data ?? new List<SpaceDto>();
    }

    public async Task<SpaceDto?> GetSpaceAsync(string id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"{_basePath}/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<StringTreeResponse<SpaceDto>>(ct);
        return result?.Data;
    }

    public async Task<SpaceDto> CreateSpaceAsync(SpaceDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(_basePath, dto, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var error = await resp.Content.ReadFromJsonAsync<StringTreeResponse<SpaceDto>>(ct);
            throw new HttpRequestException(error?.Message ?? "创建空间失败");
        }
        var result = await resp.Content.ReadFromJsonAsync<StringTreeResponse<SpaceDto>>(ct);
        return result!.Data!;
    }

    public async Task<SpaceDto?> UpdateSpaceAsync(SpaceDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"{_basePath}/{dto.Id}", dto, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!resp.IsSuccessStatusCode)
        {
            var error = await resp.Content.ReadFromJsonAsync<StringTreeResponse<SpaceDto>>(ct);
            throw new HttpRequestException(error?.Message ?? "更新空间失败");
        }
        var result = await resp.Content.ReadFromJsonAsync<StringTreeResponse<SpaceDto>>(ct);
        return result?.Data;
    }

    public async Task<bool> DeleteSpaceAsync(string id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"{_basePath}/{id}", ct);
        return resp.IsSuccessStatusCode;
    }
}

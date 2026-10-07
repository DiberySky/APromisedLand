using System.Net.Http.Json;
using TreeGraph.Shared.StringTreeSky.Contracts;

namespace TreeGraph.Blazor.Shared.StringTreeSky.Services;

/// <summary>
/// 独立非泛型 HTTP 客户端：不继承 DiberyTreeApiClient，不依赖 ITreeClientService。
/// </summary>
public class StringTreeApiClient : IStringTreeClient
{
    private readonly HttpClient _http;
    private readonly string _basePath;

    public StringTreeApiClient(HttpClient http, StringTreeSkyOptions options)
    {
        _http = http;
        _basePath = options.BasePath.TrimEnd('/');
    }

    public Task<List<StringNodeDto>> GetRootNodesAsync(CancellationToken ct = default)
        => GetListAsync($"{_basePath}/nodes/roots", ct);

    public Task<List<StringNodeDto>> GetChildrenAsync(string parentId, CancellationToken ct = default)
        => GetListAsync($"{_basePath}/nodes/children/{parentId}", ct);

    public async Task<StringNodeDto?> GetNodeAsync(string id, CancellationToken ct = default)
    {
        var resp = await _http.GetFromJsonAsync<StringTreeResponse<StringNodeDto>>(
            $"{_basePath}/nodes/{id}", ct);
        return resp?.Data;
    }

    public Task<List<StringNodeDto>> GetAncestorPathAsync(string id, CancellationToken ct = default)
        => GetListAsync($"{_basePath}/nodes/{id}/ancestors", ct);

    public async Task<StringNodeDto> CreateNodeAsync(StringNodeDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"{_basePath}/nodes", dto, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<StringTreeResponse<StringNodeDto>>(ct);
        return result!.Data!;
    }

    public async Task<StringNodeDto?> UpdateNodeAsync(StringNodeDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"{_basePath}/nodes/{dto.Id}", dto, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<StringTreeResponse<StringNodeDto>>(ct);
        return result?.Data;
    }

    public async Task<bool> DeleteNodeAsync(string id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"{_basePath}/nodes/{id}", ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> MoveNodeAsync(string id, string? newParentId, int newSortOrder, CancellationToken ct = default)
    {
        var payload = new { ParentId = newParentId, SortOrder = newSortOrder };
        var resp = await _http.PostAsJsonAsync($"{_basePath}/nodes/{id}/move", payload, ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> SortChildrenAsync(string parentId, IReadOnlyList<string> orderedIds, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"{_basePath}/nodes/{parentId}/children/sort", orderedIds, ct);
        return resp.IsSuccessStatusCode;
    }

    private async Task<List<StringNodeDto>> GetListAsync(string url, CancellationToken ct)
    {
        var resp = await _http.GetFromJsonAsync<StringTreeResponse<List<StringNodeDto>>>(url, ct);
        return resp?.Data ?? new List<StringNodeDto>();
    }
}

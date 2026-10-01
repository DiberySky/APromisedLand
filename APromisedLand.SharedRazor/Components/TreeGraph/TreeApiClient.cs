using System.Net.Http.Json;
using APromisedLand.Shared.TreeGraph.Models;

namespace APromisedLand.SharedRazor.Components.TreeGraph;

/// <summary>
/// 后端 TreeGraphApi 的 HTTP 客户端封装。BaseAddress 由 DI 容器注入时设置。
/// </summary>
public sealed class TreeApiClient
{
    private readonly HttpClient _http;

    public TreeApiClient(HttpClient http) => _http = http;

    public async Task<List<TreeNodeDto>> GetRootsAsync(int skip = 0, int take = 100,
                                                       CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<TreeNodeDto>>(
            $"api/tree/roots?skip={skip}&take={take}", ct) ?? new();

    public async Task<List<TreeNodeDto>> GetChildrenAsync(string parentId,
                                                           CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<TreeNodeDto>>(
            $"api/tree/{parentId}/children", ct) ?? new();

    public async Task<List<TreeNodeDto>> GetSubtreeAsync(string rootId,
                                                         CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<TreeNodeDto>>(
            $"api/tree/{rootId}/subtree", ct) ?? new();

    public async Task<TreeNodeDto> CreateAsync(TreeNodeDto node,
                                                CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/tree", node, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TreeNodeDto>(ct))!;
    }

    public async Task UpdateAsync(string id, TreeNodeDto node,
                                   CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/tree/{id}", node, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task MoveAsync(string nodeId, string newParentId,
                                 CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/tree/{nodeId}/move/{newParentId}",
                                          null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/tree/{id}", ct);
        resp.EnsureSuccessStatusCode();
    }
}

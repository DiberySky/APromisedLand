using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Blazor.Shared.EdgeSky.Services;

/// <summary>
/// EdgeSky 客户端（api/spaces/{spaceId}/edge/types）：边类型 CRUD（空间级，对齐 Nebula Space 语义）。
/// 返回体为统一 ApiResponse 信封，内部解包 data；失败返回错误文案供 Snackbar 展示。
/// </summary>
public class EdgeApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<EdgeApiClient> _logger;

    public EdgeApiClient(HttpClient http, ILogger<EdgeApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    private static string Base(string spaceId)
        => $"api/spaces/{Uri.EscapeDataString(spaceId)}/edge/types";

    /// <summary>列出指定空间全部边类型（含 Props）。</summary>
    public async Task<List<EdgeTypeDto>?> ListEdgeTypesAsync(string spaceId, CancellationToken ct = default)
        => await GetAsync<List<EdgeTypeDto>>(Base(spaceId), ct);

    /// <summary>按名取边类型详情；不存在返回 null。</summary>
    public async Task<EdgeTypeDto?> GetEdgeTypeAsync(string spaceId, string name, CancellationToken ct = default)
        => await GetAsync<EdgeTypeDto>($"{Base(spaceId)}/{Uri.EscapeDataString(name)}", ct);

    /// <summary>创建边类型；成功返回 (true, null)，失败返回 (false, 错误文案)。</summary>
    public async Task<(bool Ok, string? Error)> CreateEdgeTypeAsync(
        string spaceId, CreateEdgeTypeRequest req, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(Base(spaceId), req, JsonOptions, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Url} 失败", Base(spaceId));
            return (false, ex.Message);
        }
    }

    /// <summary>更新边类型中文显示名。</summary>
    public async Task<(bool Ok, string? Error)> UpdateDisplayNameAsync(
        string spaceId, string name, string? displayName, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PatchAsJsonAsync(
                $"{Base(spaceId)}/{Uri.EscapeDataString(name)}/display-name",
                new UpdateEdgeTypeDisplayNameRequest { DisplayName = displayName }, JsonOptions, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PATCH api/spaces/{SpaceId}/edge/types/{Name}/display-name 失败", spaceId, name);
            return (false, ex.Message);
        }
    }

    /// <summary>更新边类型展示序（仅 UI 排序）。</summary>
    public async Task<(bool Ok, string? Error)> UpdateDisplayOrderAsync(
        string spaceId, string name, int displayOrder, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PatchAsJsonAsync(
                $"{Base(spaceId)}/{Uri.EscapeDataString(name)}/display-order",
                new UpdateEdgeTypeDisplayOrderRequest { DisplayOrder = displayOrder }, JsonOptions, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PATCH api/spaces/{SpaceId}/edge/types/{Name}/display-order 失败", spaceId, name);
            return (false, ex.Message);
        }
    }

    /// <summary>删除边类型（有实例时服务端 409）。</summary>
    public async Task<(bool Ok, string? Error)> DeleteEdgeTypeAsync(
        string spaceId, string name, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.DeleteAsync($"{Base(spaceId)}/{Uri.EscapeDataString(name)}", ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DELETE api/spaces/{SpaceId}/edge/types/{Name} 失败", spaceId, name);
            return (false, ex.Message);
        }
    }

    /// <summary>更新边属性中文显示名。</summary>
    public async Task<(bool Ok, string? Error)> UpdatePropDisplayNameAsync(
        string spaceId, string typeName, string propName, string? displayName, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PatchAsJsonAsync(
                $"{Base(spaceId)}/{Uri.EscapeDataString(typeName)}/props/{Uri.EscapeDataString(propName)}/display-name",
                new UpdateEdgePropDisplayNameRequest { DisplayName = displayName }, JsonOptions, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PATCH api/spaces/{SpaceId}/edge/types/{Name}/props/{Prop}/display-name 失败",
                spaceId, typeName, propName);
            return (false, ex.Message);
        }
    }

    /// <summary>ALTER 边类型：属性集增/删/改类型（对齐 Nebula ALTER EDGE）。</summary>
    public async Task<(bool Ok, string? Error)> AlterEdgeTypeAsync(
        string spaceId, string name, AlterEdgeTypeRequest req, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync(
                $"{Base(spaceId)}/{Uri.EscapeDataString(name)}", req, JsonOptions, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PUT api/spaces/{SpaceId}/edge/types/{Name} 失败", spaceId, name);
            return (false, ex.Message);
        }
    }

    // ============================================================
    // 边实例（api/spaces/{spaceId}/edge/edges）
    // ============================================================

    private static string EdgesBase(string spaceId)
        => $"api/spaces/{Uri.EscapeDataString(spaceId)}/edge/edges";

    /// <summary>查询边实例；可按边类型、两端节点、节点方向（out/in/either）过滤并分页。</summary>
    public async Task<List<EdgeDto>?> QueryEdgesAsync(
        string spaceId,
        string? edgeType = null,
        string? srcVid = null,
        string? dstVid = null,
        string? nodeId = null,
        string? direction = null,
        int? limit = null,
        int? offset = null,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(edgeType)) query.Add($"edgeType={Uri.EscapeDataString(edgeType)}");
        if (!string.IsNullOrEmpty(srcVid)) query.Add($"srcVid={Uri.EscapeDataString(srcVid)}");
        if (!string.IsNullOrEmpty(dstVid)) query.Add($"dstVid={Uri.EscapeDataString(dstVid)}");
        if (!string.IsNullOrEmpty(nodeId)) query.Add($"nodeId={Uri.EscapeDataString(nodeId)}");
        if (!string.IsNullOrEmpty(direction)) query.Add($"direction={Uri.EscapeDataString(direction)}");
        if (limit is > 0) query.Add($"limit={limit}");
        if (offset is > 0) query.Add($"offset={offset}");

        var url = EdgesBase(spaceId);
        if (query.Count > 0) url += "?" + string.Join("&", query);
        return await GetAsync<List<EdgeDto>>(url, ct);
    }

    /// <summary>按五元组取单条边；不存在返回 null。</summary>
    public async Task<EdgeDto?> GetEdgeAsync(
        string spaceId, string edgeType, string srcVid, long rank, string dstVid,
        CancellationToken ct = default)
        => await GetAsync<EdgeDto>(EdgeUrl(spaceId, edgeType, srcVid, rank, dstVid), ct);

    /// <summary>覆盖式 UPSERT（对齐 INSERT EDGE）；返回 (ok, 错误文案)。</summary>
    public async Task<(bool Ok, string? Error)> UpsertEdgeAsync(
        string spaceId, string edgeType, string srcVid, long rank, string dstVid,
        UpsertEdgeRequest req, CancellationToken ct = default)
    {
        var url = EdgeUrl(spaceId, edgeType, srcVid, rank, dstVid);
        try
        {
            var resp = await _http.PutAsJsonAsync(url, req, JsonOptions, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PUT {Url} 失败", url);
            return (false, ex.Message);
        }
    }

    /// <summary>部分更新（对齐 UPDATE EDGE SET）；返回 (ok, 错误文案)。</summary>
    public async Task<(bool Ok, string? Error)> PatchEdgeAsync(
        string spaceId, string edgeType, string srcVid, long rank, string dstVid,
        PatchEdgeRequest req, CancellationToken ct = default)
    {
        var url = EdgeUrl(spaceId, edgeType, srcVid, rank, dstVid);
        try
        {
            var resp = await _http.PatchAsJsonAsync(url, req, JsonOptions, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PATCH {Url} 失败", url);
            return (false, ex.Message);
        }
    }

    /// <summary>按五元组删除边（对齐 DELETE EDGE）。</summary>
    public async Task<(bool Ok, string? Error)> DeleteEdgeAsync(
        string spaceId, string edgeType, string srcVid, long rank, string dstVid,
        CancellationToken ct = default)
    {
        var url = EdgeUrl(spaceId, edgeType, srcVid, rank, dstVid);
        try
        {
            var resp = await _http.DeleteAsync(url, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DELETE {Url} 失败", url);
            return (false, ex.Message);
        }
    }

    /// <summary>解析节点所属图空间（api/edge/node-space/{nodeId}）；无归属节点返回 null。</summary>
    public async Task<NodeSpaceDto?> GetNodeSpaceAsync(string nodeId, CancellationToken ct = default)
        => await GetAsync<NodeSpaceDto>($"api/edge/node-space/{Uri.EscapeDataString(nodeId)}", ct);

    /// <summary>按名称搜索节点（api/edge/nodes?search=），用于选择对端节点。</summary>
    public async Task<List<NodeLookupDto>> SearchNodesAsync(string? search, CancellationToken ct = default)
    {
        var url = "api/edge/nodes";
        if (!string.IsNullOrWhiteSpace(search)) url += $"?search={Uri.EscapeDataString(search)}";
        return await GetAsync<List<NodeLookupDto>>(url, ct) ?? [];
    }

    private static string EdgeUrl(string spaceId, string edgeType, string srcVid, long rank, string dstVid)
        => $"{EdgesBase(spaceId)}/{Uri.EscapeDataString(edgeType)}/{Uri.EscapeDataString(srcVid)}"
           + $"/{rank}/{Uri.EscapeDataString(dstVid)}";

    // ============================================================

    private async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct) where T : class
    {
        try
        {
            var resp = await _http.GetAsync(relativeUrl, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET {Url} → {Status}", relativeUrl, resp.StatusCode);
                return default;
            }
            var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions, ct);
            return envelope is not null && !envelope.Success ? default : envelope?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Url} 失败", relativeUrl);
            return default;
        }
    }

    /// <summary>从非 2xx 响应提取错误文案（兼容 ApiResponse 信封与裸 error 对象）。</summary>
    private static async Task<string> ExtractErrorAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                return msg.GetString() ?? $"HTTP {(int)resp.StatusCode}";
            if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                return err.GetString() ?? $"HTTP {(int)resp.StatusCode}";
            return $"HTTP {(int)resp.StatusCode}";
        }
        catch
        {
            return $"HTTP {(int)resp.StatusCode}";
        }
    }
}

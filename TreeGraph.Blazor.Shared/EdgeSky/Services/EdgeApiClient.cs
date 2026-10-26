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

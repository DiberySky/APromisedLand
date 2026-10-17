using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Blazor.Shared.EdgeSky.Services;

/// <summary>
/// Edge（关系/边）API 客户端：边类型元数据 + 边实例 CRUD + 图查询。
///
/// ★ 所有实体 ID / 元数据 ID 均为 GUID 字符串（36 字符）。
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

    // ============================================================
    // Edge Type（元数据）
    // ============================================================

    public async Task<IReadOnlyList<EdgeTypeDto>?> ListEdgeTypesAsync(
        CancellationToken ct = default)
        => await GetAsync<IReadOnlyList<EdgeTypeDto>>(
            "api/eav/metadata/edge-types", ct);

    public Task<EdgeTypeDto?> GetEdgeTypeAsync(
        string edgeTypeId, CancellationToken ct = default)
        => GetAsync<EdgeTypeDto>(
            $"api/eav/metadata/edge-types/{edgeTypeId}", ct);

    public Task<string?> CreateEdgeTypeAsync(
        CreateEdgeTypeRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/metadata/edge-types", request, "edgeTypeId", ct);

    public Task<(bool Ok, string? Error)> UpdateEdgeTypeAsync(
        string edgeTypeId, UpdateEdgeTypeRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/metadata/edge-types/{edgeTypeId}", request, ct);

    public Task<(bool Ok, string? Error)> DeleteEdgeTypeAsync(
        string edgeTypeId, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/metadata/edge-types/{edgeTypeId}", ct);

    // ============================================================
    // Edge Instance
    // ============================================================

    public async Task<IReadOnlyList<EdgeInstanceDto>?> ListEdgesAsync(
        string edgeTypeId, CancellationToken ct = default)
        => await GetAsync<IReadOnlyList<EdgeInstanceDto>>(
            $"api/eav/edges/{Uri.EscapeDataString(edgeTypeId)}", ct);

    public Task<EdgeInstanceDto?> GetEdgeAsync(
        string edgeTypeId, string edgeId, CancellationToken ct = default)
        => GetAsync<EdgeInstanceDto>(
            $"api/eav/edges/{Uri.EscapeDataString(edgeTypeId)}/{Uri.EscapeDataString(edgeId)}",
            ct);

    public Task<string?> CreateEdgeAsync(
        string edgeTypeId, CreateEdgeRequest request, CancellationToken ct = default)
        => PostForIdAsync(
            $"api/eav/edges/{Uri.EscapeDataString(edgeTypeId)}",
            request, "edgeId", ct);

    public Task<(bool Ok, string? Error)> UpdateEdgeAsync(
        string edgeTypeId, string edgeId,
        UpdateEdgeRequest request, CancellationToken ct = default)
        => PutAsync(
            $"api/eav/edges/{Uri.EscapeDataString(edgeTypeId)}/{Uri.EscapeDataString(edgeId)}",
            request, ct);

    public Task<(bool Ok, string? Error)> DeleteEdgeAsync(
        string edgeTypeId, string edgeId, CancellationToken ct = default)
        => DeleteWithErrorAsync(
            $"api/eav/edges/{Uri.EscapeDataString(edgeTypeId)}/{Uri.EscapeDataString(edgeId)}",
            ct);

    // ============================================================
    // 图查询
    // ============================================================

    public async Task<IReadOnlyList<EdgeInstanceDto>?> FindOutgoingEdgesAsync(
        string edgeTypeId, string nodeId, CancellationToken ct = default)
        => await GetAsync<IReadOnlyList<EdgeInstanceDto>>(
            $"api/eav/edges/{Uri.EscapeDataString(edgeTypeId)}" +
            $"/from/{Uri.EscapeDataString(nodeId)}", ct);

    public async Task<IReadOnlyList<EdgeInstanceDto>?> FindIncomingEdgesAsync(
        string edgeTypeId, string nodeId, CancellationToken ct = default)
        => await GetAsync<IReadOnlyList<EdgeInstanceDto>>(
            $"api/eav/edges/{Uri.EscapeDataString(edgeTypeId)}" +
            $"/to/{Uri.EscapeDataString(nodeId)}", ct);

    public async Task<EdgeTraverseResult?> TraverseEdgesAsync(
        string edgeTypeId, EdgeTraverseRequest request, CancellationToken ct = default)
        => await PostAsync<EdgeTraverseResult>(
            $"api/eav/edges/{Uri.EscapeDataString(edgeTypeId)}/traverse",
            request, ct);

    // ============================================================
    // 通用助手（与 EavApiClient 同款实现）
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
            // ★ 统一 ApiResponse 信封：返回体 { success, message, data }，这里解包 data
            var envelope = await resp.Content
                .ReadFromJsonAsync<ApiResponse<T>>(JsonOptions, ct);
            return envelope is not null && !envelope.Success
                ? default
                : envelope?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Url} 失败", relativeUrl);
            return default;
        }
    }

    private async Task<T?> PostAsync<T>(
        string relativeUrl, object body, CancellationToken ct) where T : class
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(relativeUrl, body, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("POST {Url} → {Status}", relativeUrl, resp.StatusCode);
                return default;
            }
            // ★ 统一 ApiResponse 信封：解包 data
            var envelope = await resp.Content
                .ReadFromJsonAsync<ApiResponse<T>>(JsonOptions, ct);
            return envelope is not null && !envelope.Success
                ? default
                : envelope?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Url} 失败", relativeUrl);
            return default;
        }
    }

    private async Task<(bool Ok, string? Error)> PutAsync(
        string relativeUrl, object body, CancellationToken ct)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync(relativeUrl, body, JsonOptions, ct);
            if (resp.IsSuccessStatusCode) return (true, null);

            var msg = await ExtractErrorAsync(resp, ct);
            _logger.LogWarning("PUT {Url} → {Status}: {Error}",
                relativeUrl, resp.StatusCode, msg);
            return (false, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PUT {Url} 失败", relativeUrl);
            return (false, ex.Message);
        }
    }

    private async Task<(bool Ok, string? Error)> DeleteWithErrorAsync(
        string relativeUrl, CancellationToken ct)
    {
        try
        {
            var resp = await _http.DeleteAsync(relativeUrl, ct);
            if (resp.IsSuccessStatusCode) return (true, null);

            var msg = await ExtractErrorAsync(resp, ct);
            _logger.LogWarning("DELETE {Url} → {Status}: {Error}",
                relativeUrl, resp.StatusCode, msg);
            return (false, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DELETE {Url} 失败", relativeUrl);
            return (false, ex.Message);
        }
    }

    private static async Task<string?> ExtractErrorAsync(
        HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            // ★ 错误体有两种形态：
            //   1) 统一信封 { success, message, data: { error?, errors? } }
            //   2) 部分端点直接返回的裸对象 { error } / { message }
            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
            if (doc.ValueKind != JsonValueKind.Object) return resp.ReasonPhrase;

            // 信封形态取 data；裸形态无 data，退回根对象本身
            var body = doc.TryGetProperty("data", out var data)
                       && data.ValueKind == JsonValueKind.Object
                ? data
                : doc;

            string? detail = null;
            if (body.TryGetProperty("error", out var err)
                && err.ValueKind == JsonValueKind.String)
            {
                detail = err.GetString();
            }

            if (body.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array)
            {
                var parts = new List<string>();
                foreach (var item in errors.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var field = item.TryGetProperty("field", out var f)
                        && f.ValueKind == JsonValueKind.String
                        ? f.GetString()
                        : null;
                    var msg = item.TryGetProperty("message", out var m)
                        && m.ValueKind == JsonValueKind.String
                        ? m.GetString()
                        : item.ToString();
                    parts.Add(string.IsNullOrEmpty(field) ? msg! : $"{field}: {msg}");
                }
                if (parts.Count > 0) detail = string.Join("; ", parts);
            }

            if (!string.IsNullOrEmpty(detail)) return detail;

            // message 在根上（信封形态与裸形态都命中）
            if (doc.TryGetProperty("message", out var rootMsg)
                && rootMsg.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(rootMsg.GetString()))
            {
                return rootMsg.GetString();
            }

            return resp.ReasonPhrase;
        }
        catch
        {
            return resp.ReasonPhrase;
        }
    }

    /// <summary>
    /// ★ 按属性名取 ID。ID 值从 JSON 数字改为字符串。
    /// </summary>
    private async Task<string?> PostForIdAsync(
        string relativeUrl, object body, string idPropertyName,
        CancellationToken ct)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(relativeUrl, body, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var text = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("POST {Url} → {Status}: {Body}",
                    relativeUrl, resp.StatusCode, text);
                return null;
            }

            // ★ 统一信封：ID 位于 data 节点内
            var envelope = await resp.Content
                .ReadFromJsonAsync<ApiResponse<JsonElement>>(JsonOptions, ct);
            if (envelope?.Data.ValueKind == JsonValueKind.Object
                && envelope.Data.TryGetProperty(idPropertyName, out var p)
                && p.ValueKind == JsonValueKind.String)
            {
                return p.GetString();
            }

            _logger.LogWarning(
                "POST {Url} → 响应缺少或类型不符 {IdProperty}: {Body}",
                relativeUrl, idPropertyName, envelope?.Data.GetRawText() ?? "(空响应)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Url} 失败", relativeUrl);
            return null;
        }
    }
}

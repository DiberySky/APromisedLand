using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TreeGraph.Shared.LineSky.Dtos;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Blazor.Shared.LineSky.Services;

/// <summary>
/// 线路结构（Line）API 客户端：建线、上下游/全路径查询、拓扑校验、连通分量。
/// </summary>
public class LineApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<LineApiClient> _logger;

    public LineApiClient(HttpClient http, ILogger<LineApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public Task<List<EdgeInstanceDto>?> CreateLineAsync(
        string edgeTypeId, CreateLineRequest request, CancellationToken ct = default)
        => PostAsync<List<EdgeInstanceDto>>(
            $"api/eav/lines/{Uri.EscapeDataString(edgeTypeId)}/create",
            request, ct);

    public Task<LinePathResult?> GetLineUpstreamAsync(
        string edgeTypeId, string nodeId, CancellationToken ct = default)
        => GetAsync<LinePathResult>(
            $"api/eav/lines/{Uri.EscapeDataString(edgeTypeId)}/upstream/{Uri.EscapeDataString(nodeId)}",
            ct);

    public Task<LinePathResult?> GetLineDownstreamAsync(
        string edgeTypeId, string nodeId, CancellationToken ct = default)
        => GetAsync<LinePathResult>(
            $"api/eav/lines/{Uri.EscapeDataString(edgeTypeId)}/downstream/{Uri.EscapeDataString(nodeId)}",
            ct);

    public Task<LinePathResult?> GetLineFullPathAsync(
        string edgeTypeId, string nodeId, CancellationToken ct = default)
        => GetAsync<LinePathResult>(
            $"api/eav/lines/{Uri.EscapeDataString(edgeTypeId)}/path/{Uri.EscapeDataString(nodeId)}",
            ct);

    public Task<LineValidationResult?> ValidateLineTopologyAsync(
        string edgeTypeId, CancellationToken ct = default)
        => GetAsync<LineValidationResult>(
            $"api/eav/lines/{Uri.EscapeDataString(edgeTypeId)}/validate",
            ct);

    public Task<IReadOnlyList<LineConnectedComponent>?> GetLineComponentsAsync(
        string edgeTypeId, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<LineConnectedComponent>>(
            $"api/eav/lines/{Uri.EscapeDataString(edgeTypeId)}/components",
            ct);

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
}

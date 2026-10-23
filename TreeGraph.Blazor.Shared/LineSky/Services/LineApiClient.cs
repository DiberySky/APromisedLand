using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TreeGraph.Shared.LineSky.Dtos;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Blazor.Shared.LineSky.Services;

/// <summary>
/// 线路（Line）API 客户端：结构读写、上下游/全路径查询、拓扑校验、连通分量。
/// </summary>
public class LineApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly ILogger<LineApiClient> _logger;

    public LineApiClient(HttpClient http, ILogger<LineApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    // ============================================================
    // 线路结构（LineStructure）
    // ============================================================

    /// <summary>读取线路结构；线路不存在返回 null，无结构返回 Exists=false + 候选节点。</summary>
    public Task<LineStructureDto?> GetStructureAsync(string lineId, CancellationToken ct = default)
        => GetAsync<LineStructureDto>(StructureUrl(lineId), ct);

    /// <summary>创建结构（已存在则返回错误）。</summary>
    public Task<(LineStructureDto? Data, string? Error)> CreateStructureAsync(
        string lineId, LineStructureRequest request, CancellationToken ct = default)
        => SendStructureAsync(HttpMethod.Post, StructureUrl(lineId), request, ct);

    /// <summary>全量替换结构（空段请求等价于清空）。</summary>
    public Task<(LineStructureDto? Data, string? Error)> ReplaceStructureAsync(
        string lineId, LineStructureRequest request, CancellationToken ct = default)
        => SendStructureAsync(HttpMethod.Put, StructureUrl(lineId), request, ct);

    public Task<(LineStructureDto? Data, string? Error)> AddSegmentAsync(
        string lineId, AddLineSegmentRequest request, CancellationToken ct = default)
        => SendStructureAsync(HttpMethod.Post, $"{StructureUrl(lineId)}/segments", request, ct);

    public Task<(LineStructureDto? Data, string? Error)> DeleteSegmentAsync(
        string lineId, string edgeId, CancellationToken ct = default)
        => SendStructureAsync(
            HttpMethod.Delete,
            $"{StructureUrl(lineId)}/segments/{Uri.EscapeDataString(edgeId)}",
            null, ct);

    public Task<(LineStructureDto? Data, string? Error)> UpsertTerminalAsync(
        string lineId, AddLineTerminalRequest request, CancellationToken ct = default)
        => SendStructureAsync(HttpMethod.Post, $"{StructureUrl(lineId)}/terminals", request, ct);

    public Task<(LineStructureDto? Data, string? Error)> DeleteTerminalAsync(
        string lineId, string nodeId, CancellationToken ct = default)
        => SendStructureAsync(
            HttpMethod.Delete,
            $"{StructureUrl(lineId)}/terminals/{Uri.EscapeDataString(nodeId)}",
            null, ct);

    private static string StructureUrl(string lineId)
        => $"api/eav/lines/{Uri.EscapeDataString(lineId)}/structure";

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

    /// <summary>读取全部线路模板（UI 层下拉预填用）。</summary>
    public Task<IReadOnlyList<LineTemplateDto>?> GetLineTemplatesAsync(CancellationToken ct = default)
        => GetAsync<IReadOnlyList<LineTemplateDto>>("api/eav/line-templates", ct);

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
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("GET {Url} → {Status}: {Body}", relativeUrl, resp.StatusCode, body);
                throw new HttpRequestException(
                    $"GET {relativeUrl} → HTTP {(int)resp.StatusCode}: {body}");
            }
            // ★ 统一 ApiResponse 信封：返回体 { success, message, data }，这里解包 data
            var envelope = await resp.Content
                .ReadFromJsonAsync<ApiResponse<T>>(JsonOptions, ct);
            if (envelope is not null && !envelope.Success)
            {
                throw new InvalidOperationException(
                    $"GET {relativeUrl} 返回 success=false: {envelope.Message}");
            }
            return envelope?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Url} 失败", relativeUrl);
            throw;
        }
    }

    /// <summary>
    /// 结构写操作：发送请求并解包信封；失败时回传服务端 message（4xx 业务冲突提示）。
    /// </summary>
    private async Task<(LineStructureDto? Data, string? Error)> SendStructureAsync(
        HttpMethod method, string relativeUrl, object? body, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(method, relativeUrl);
            if (body is not null)
                req.Content = JsonContent.Create(body, options: JsonOptions);

            var resp = await _http.SendAsync(req, ct);
            var envelope = await resp.Content
                .ReadFromJsonAsync<ApiResponse<LineStructureDto>>(JsonOptions, ct);

            if (!resp.IsSuccessStatusCode || envelope is null || !envelope.Success)
            {
                _logger.LogWarning("{Method} {Url} → {Status}", method, relativeUrl, resp.StatusCode);
                return (null, envelope?.Message ?? $"请求失败（HTTP {(int)resp.StatusCode}）");
            }
            return (envelope.Data, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Method} {Url} 失败", method, relativeUrl);
            return (null, ex.Message);
        }
    }
}

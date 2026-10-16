using System.Text.Json;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.MafWfApi.Services;

/// <summary>
/// TreeGraph.Api 语义搜索客户端（/api/search/semantic）。
/// 响应为全局信封 { success, data }，此处直接解析 data.items。
/// </summary>
public sealed class SearchApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<SearchApiClient> _logger;

    public SearchApiClient(HttpClient http, ILogger<SearchApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// 语义搜索节点。
    /// 服务不可用时返回 null（调用方降级处理，不抛异常）。
    /// </summary>
    public async Task<SemanticSearchResult?> SearchAsync(
        string query, int topK = 5, string? spaceId = null,
        CancellationToken ct = default)
    {
        try
        {
            var request = new { query, topK, spaceId };
            var response = await _http.PostAsJsonAsync(
                "api/search/semantic", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "语义搜索服务异常：{Status}", response.StatusCode);
                return null;
            }

            var json = await response.Content
                .ReadFromJsonAsync<JsonElement>(ct);

            // 信封：{ success, data: { query, total, items } }
            if (!json.TryGetProperty("data", out var data))
                return null;

            var items = data.TryGetProperty("items", out var arr)
                ? arr.Deserialize<List<SemanticSearchItemDto>>(JsonOptions) ?? []
                : [];

            return new SemanticSearchResult(items);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "语义搜索服务不可用");
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("语义搜索超时");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "语义搜索响应解析失败");
            return null;
        }
    }
}

public sealed record SemanticSearchResult(IReadOnlyList<SemanticSearchItemDto> Items);

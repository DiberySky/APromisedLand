using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace TreeGraph.MafWfApi.Infrastructure;

/// <summary>TreeGraph.NebulaFastApi /api/v1/graph/query 返回结果。</summary>
public sealed class NebulaQueryResult
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("error_code")] public int ErrorCode { get; set; }
    [JsonPropertyName("error_msg")] public string? ErrorMsg { get; set; }
    [JsonPropertyName("data")] public List<Dictionary<string, JsonElement>>? Data { get; set; }
    [JsonPropertyName("columns")] public List<string>? Columns { get; set; }
    [JsonPropertyName("row_count")] public int RowCount { get; set; }
}

/// <summary>
/// TreeGraph.NebulaFastApi REST 客户端（只读 nGQL 查询）。
/// 对应 Python 侧 QueryResult 序列化（app/models/schemas.py）。
/// </summary>
public sealed class NebulaGraphRestClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly NebulaGraphOptions _options;
    private readonly ILogger<NebulaGraphRestClient> _logger;

    public string Space => _options.Space;

    public NebulaGraphRestClient(
        IHttpClientFactory factory,
        IOptions<NebulaGraphOptions> options,
        ILogger<NebulaGraphRestClient> logger)
    {
        _options = options.Value;
        _logger = logger;

        var endpoint = string.IsNullOrWhiteSpace(_options.Endpoint)
            ? NebulaGraphOptions.DefaultEndpoint
            : _options.Endpoint!;

        _http = factory.CreateClient("NebulaFastApi");
        _http.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");

        _logger.LogInformation(
            "NebulaGraphRestClient 初始化：endpoint = {Endpoint}, space = {Space}",
            _http.BaseAddress, _options.Space);
    }

    /// <summary>
    /// 执行 nGQL 查询。
    /// HTTP 非 2xx 抛 <see cref="HttpRequestException"/>；
    /// NebulaGraph 返回 success=false 抛 <see cref="InvalidOperationException"/>。
    /// </summary>
    public async Task<NebulaQueryResult> QueryAsync(string ngql, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            "api/v1/graph/query",
            new { space = _options.Space, query = ngql }, JsonOpts, ct);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<NebulaQueryResult>(JsonOpts, ct);
        if (result is null)
            throw new InvalidOperationException("NebulaFastApi 返回空响应");
        if (!result.Success)
            throw new InvalidOperationException($"nGQL 执行失败: {result.ErrorMsg} | {ngql}");
        return result;
    }
}

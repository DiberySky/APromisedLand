using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace MAFWorkFlowApi.Infrastructure;

public sealed class LiteGraphRestClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ILogger<LiteGraphRestClient> _logger;

    public Guid TenantGuid { get; }
    public string Endpoint { get; }

    public LiteGraphRestClient(
        IHttpClientFactory factory,
        IOptions<LiteGraphOptions> options,
        ILogger<LiteGraphRestClient> logger)
    {
        var opt = options.Value;
        var endpoint = LiteGraphEndpointResolver.Resolve(opt, logger);

        if (!Guid.TryParse(opt.TenantGuid, out var tenantGuid))
            throw new InvalidOperationException(
                $"LiteGraph:TenantGuid 不是有效 GUID：'{opt.TenantGuid}'");

        TenantGuid = tenantGuid;
        Endpoint = endpoint;

        _http = factory.CreateClient("LiteGraph");
        _http.BaseAddress = new Uri(endpoint);
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", opt.ApiKey);
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        _logger = logger;

        _logger.LogInformation(
            "LiteGraphRestClient 初始化：endpoint = {Endpoint}, tenant = {Tenant}",
            endpoint, tenantGuid);

        // ★ 使用 Once 版本 —— Interlocked 保证全进程只输出一次
        LiteGraphEndpointResolver.DescribeResolutionOnce(opt, logger);
    }

    // ─── 通用 ──────────────────────────────────────────

    public async Task<JsonElement?> GetAsync(string path, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(path, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(resp, "GET", path, ct);

        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length == 0) return null;

        var element = JsonSerializer.Deserialize<JsonElement>(bytes, JsonOpts);
        return element.Clone();
    }

    public Task<JsonElement> PostAsync(
        string path, object body, CancellationToken ct = default)
        => SendJsonAsync(HttpMethod.Post, path, body, ct);

    public Task<JsonElement> PutAsync(
        string path, object body, CancellationToken ct = default)
        => SendJsonAsync(HttpMethod.Put, path, body, ct);

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        using var resp = await _http.DeleteAsync(path, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return;
        await EnsureSuccessAsync(resp, "DELETE", path, ct);
    }

    private async Task<JsonElement> SendJsonAsync(
        HttpMethod method, string path, object body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body, options: JsonOpts)
        };
        using var resp = await _http.SendAsync(req, ct);
        await EnsureSuccessAsync(resp, method.Method, path, ct);

        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length == 0) return default;

        var element = JsonSerializer.Deserialize<JsonElement>(bytes, JsonOpts);
        return element.Clone();
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage resp, string method, string path, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;

        var body = await resp.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"{method} {path} 失败：HTTP {(int)resp.StatusCode} {body}",
            null, resp.StatusCode);
    }
}
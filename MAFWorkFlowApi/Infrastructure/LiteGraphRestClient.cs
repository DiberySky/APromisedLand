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

    public LiteGraphRestClient(
        IHttpClientFactory factory,
        IOptions<LiteGraphOptions> options,
        ILogger<LiteGraphRestClient> logger)
    {
        var opt = options.Value;
        var endpoint = LiteGraphEndpointResolver.Resolve(opt);

        if (!Guid.TryParse(opt.TenantGuid, out var tenantGuid))
            throw new InvalidOperationException(
                $"LiteGraph:TenantGuid 不是有效 GUID：'{opt.TenantGuid}'");

        TenantGuid = tenantGuid;
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
    }

    // ─── 通用 ──────────────────────────────────────────

    public async Task<JsonElement?> GetAsync(string path, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(path, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(resp, "GET", path, ct);
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        return bytes.Length == 0 ? null : JsonSerializer.Deserialize<JsonElement>(bytes, JsonOpts);
    }

    public Task<JsonElement> PostAsync(string path, object body, CancellationToken ct = default)
        => SendJsonAsync(HttpMethod.Post, path, body, ct);

    /// <summary>
    /// ★ 修改：返回 Task&lt;JsonElement&gt;（与 PostAsync 一致），
    /// 调用方可以 await 忽略返回值，也可以 var 接收。
    /// </summary>
    public Task<JsonElement> PutAsync(string path, object body, CancellationToken ct = default)
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
        return bytes.Length == 0 ? default : JsonSerializer.Deserialize<JsonElement>(bytes, JsonOpts);
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
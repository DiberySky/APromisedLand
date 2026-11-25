using System.Diagnostics.Metrics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TreeGraph.IdentityAdmin.Options;
using TreeGraph.IdentityAdmin.Token;

namespace TreeGraph.IdentityAdmin.Keycloak;

/// <summary>
/// Keycloak Admin REST API 客户端。统一拼接 realm 前缀、注入管理令牌，
/// 并在 401 时强制刷新令牌后重试一次。
/// </summary>
public sealed class KeycloakAdminClient
{
    private static readonly Meter Meter = new("TreeGraph.IdentityAdmin", "1.0.0");
    private static readonly Counter<long> RequestsCounter =
        Meter.CreateCounter<long>("keycloak_admin_requests_total");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAdminTokenProvider _tokenProvider;
    private readonly IOptionsMonitor<KeycloakAdminOptions> _options;
    private readonly ILogger<KeycloakAdminClient> _logger;

    public KeycloakAdminClient(
        IHttpClientFactory httpClientFactory,
        IAdminTokenProvider tokenProvider,
        IOptionsMonitor<KeycloakAdminOptions> options,
        ILogger<KeycloakAdminClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _options = options;
        _logger = logger;
    }

    /// <summary>管理 API 基地址（不含 realm）。</summary>
    public string AdminBaseUrl => _options.CurrentValue.AdminBaseUrl.TrimEnd('/');

    /// <summary>realm 名称。</summary>
    public string Realm => _options.CurrentValue.Realm;

    /// <summary>发送 JSON 请求。relativePath 相对于 <c>/admin/realms/{realm}/</c>。</summary>
    public Task<KeycloakResponse> SendAsync(
        HttpMethod method,
        string relativePath,
        object? body = null,
        string? module = null,
        CancellationToken ct = default)
    {
        var json = body is null ? null : JsonSerializer.Serialize(body);
        return SendInternalAsync(
            method,
            BuildRealmUrl(relativePath),
            json is null ? null : () => new StringContent(json, Encoding.UTF8, "application/json"),
            module,
            ct);
    }

    /// <summary>发送 form-urlencoded 请求（如 LDAP 连接测试）。</summary>
    public Task<KeycloakResponse> SendFormAsync(
        HttpMethod method,
        string relativePath,
        IEnumerable<KeyValuePair<string, string>> fields,
        string? module = null,
        CancellationToken ct = default)
    {
        var payload = fields.ToArray();
        return SendInternalAsync(
            method,
            BuildRealmUrl(relativePath),
            () => new FormUrlEncodedContent(payload),
            module,
            ct);
    }

    /// <summary>发送 <c>/admin</c> 下、不属于 realm 的请求（如 serverinfo）。</summary>
    public Task<KeycloakResponse> SendAdminRootAsync(
        HttpMethod method,
        string path,
        string? module = null,
        CancellationToken ct = default)
        => SendInternalAsync(
            method,
            string.Concat(AdminBaseUrl, "/admin/", path.TrimStart('/')),
            null,
            module,
            ct);

    public Task<KeycloakResponse> GetAsync(
        string relativePath,
        string? module = null,
        CancellationToken ct = default)
        => SendAsync(HttpMethod.Get, relativePath, null, module, ct);

    public async Task<T?> GetJsonAsync<T>(
        string relativePath,
        string? module = null,
        CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Get, relativePath, null, module, ct).ConfigureAwait(false);
        return response.IsSuccess ? response.Deserialize<T>() : default;
    }

    /// <summary>拼接查询字符串；null 或空值会被忽略并做 URL 编码。</summary>
    public static string BuildQuery(params (string Key, string? Value)[] parameters)
    {
        var parts = new List<string>();
        foreach (var (key, value) in parameters)
        {
            if (string.IsNullOrEmpty(value))
                continue;

            parts.Add(string.Concat(Uri.EscapeDataString(key), "=", Uri.EscapeDataString(value)));
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }

    private string BuildRealmUrl(string relativePath)
    {
        var baseUrl = string.Concat(AdminBaseUrl, "/admin/realms/", Realm);
        var path = relativePath.TrimStart('/');
        return path.Length == 0 ? baseUrl : string.Concat(baseUrl, "/", path);
    }

    private async Task<KeycloakResponse> SendInternalAsync(
        HttpMethod method,
        string url,
        Func<HttpContent?>? contentFactory,
        string? module,
        CancellationToken ct)
    {
        var response = await SendCoreAsync(method, url, contentFactory?.Invoke(), module, ct).ConfigureAwait(false);

        if (response.StatusCode == 401)
        {
            _logger.LogWarning("管理 API 返回 401，刷新令牌后重试一次：{Method} {Url}", method, url);
            _tokenProvider.Invalidate();
            response = await SendCoreAsync(method, url, contentFactory?.Invoke(), module, ct)
                .ConfigureAwait(false);

            if (response.StatusCode == 401)
                throw new KeycloakUnauthorizedException($"管理 API 在刷新令牌后仍返回 401：{method} {url}");
        }

        return response;
    }

    private async Task<KeycloakResponse> SendCoreAsync(
        HttpMethod method,
        string url,
        HttpContent? content,
        string? module,
        CancellationToken ct)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(ct).ConfigureAwait(false);
        var client = _httpClientFactory.CreateClient(KeycloakClientNames.Admin);

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content is not null)
            request.Content = content;

        try
        {
            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var location = response.Headers.Location?.ToString();

            RequestsCounter.Add(1, new KeyValuePair<string, object?>("module", module ?? "unknown"),
                new KeyValuePair<string, object?>("status", (int)response.StatusCode));

            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning(
                    "管理 API 调用失败：{Method} {Url} → HTTP {Status}",
                    method,
                    url,
                    (int)response.StatusCode);
            }

            return new KeycloakResponse((int)response.StatusCode, body, location);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            RequestsCounter.Add(1, new KeyValuePair<string, object?>("module", module ?? "unknown"),
                new KeyValuePair<string, object?>("status", 0));
            _logger.LogError(ex, "管理 API 调用异常：{Method} {Url}", method, url);
            throw;
        }
    }
}

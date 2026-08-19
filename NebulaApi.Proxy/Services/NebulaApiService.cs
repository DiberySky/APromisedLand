using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaApi.Proxy.Models;

namespace NebulaApi.Proxy.Services;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper that forwards requests to
/// the deployed FastAPI + nebula-python service and unpacks the
/// upstream <c>{code, message, data, meta?}</c> envelope into a
/// <see cref="ProxyResult"/>. All non-2xx upstream responses (and any
/// transport / parse failures) are surfaced via the same envelope so
/// the controllers only need <c>StatusCode(StatusCode, Body)</c>.
/// </summary>
public sealed class NebulaFastApiService
{
    private readonly HttpClient _http;
    private readonly NebulaFastApiOptions _options;
    private readonly ILogger<NebulaFastApiService> _logger;

    /// <summary>
    /// Shared JSON options: snake_case naming (matching the FastAPI
    /// Pydantic schemas) plus null-value omission so optional fields
    /// are not echoed as <c>null</c> to the upstream service.
    /// </summary>
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public NebulaFastApiService(
        HttpClient http,
        IOptions<NebulaFastApiOptions> options,
        ILogger<NebulaFastApiService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + '/');
        }
        if (_http.Timeout != TimeSpan.FromSeconds(_options.TimeoutSeconds))
        {
            _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }
        if (!string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiToken);
        }
    }

    // ----------------------------------------------------------------- //
    // Public verbs
    // ----------------------------------------------------------------- //

    public Task<ProxyResult> GetAsync(
        string path,
        IEnumerable<KeyValuePair<string, string?>>? query = null,
        CancellationToken ct = default)
        => SendAsync(HttpMethod.Get, path, query, body: (object?)null, ct);

    public Task<ProxyResult> PostAsync(
        string path,
        IEnumerable<KeyValuePair<string, string?>>? query = null,
        CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, path, query, body: (object?)null, ct);

    public Task<ProxyResult> PostAsync<TBody>(
        string path,
        TBody body,
        IEnumerable<KeyValuePair<string, string?>>? query = null,
        CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, path, query, body, ct);

    public Task<ProxyResult> PutAsync<TBody>(
        string path,
        TBody body,
        IEnumerable<KeyValuePair<string, string?>>? query = null,
        CancellationToken ct = default)
        => SendAsync(HttpMethod.Put, path, query, body, ct);

    public Task<ProxyResult> DeleteAsync(
        string path,
        IEnumerable<KeyValuePair<string, string?>>? query = null,
        CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, path, query, body: (object?)null, ct);

    /// <summary>
    /// DELETE with a JSON request body. FastAPI declares the
    /// <c>DELETE /spaces/{space}/vertices</c> and
    /// <c>DELETE /spaces/{space}/edges</c> endpoints with a
    /// <c>[FromBody]</c> payload, so the proxy needs to forward that
    /// body upstream.
    /// </summary>
    public Task<ProxyResult> DeleteAsync<TBody>(
        string path,
        TBody body,
        IEnumerable<KeyValuePair<string, string?>>? query = null,
        CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, path, query, body, ct);

    // ----------------------------------------------------------------- //
    // Core send + parse pipeline
    // ----------------------------------------------------------------- //

    private async Task<ProxyResult> SendAsync<TBody>(
        HttpMethod method,
        string path,
        IEnumerable<KeyValuePair<string, string?>>? query,
        TBody? body,
        CancellationToken ct)
    {
        // Build the relative URI (path already URL-encoded by caller).
        var uri = path.StartsWith('/') ? path[1..] : path;
        if (query is not null)
        {
            var qs = BuildQuery(query);
            if (qs.Length > 0)
            {
                uri = uri.Contains('?') ? uri + "&" + qs[1..] : uri + qs;
            }
        }

        using var req = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            req.Content = JsonContent.Create(body, options: JsonOptions);
        }

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Upstream FastAPI call timed out: {Method} {Uri}", method, uri);
            return ProxyResult.BadGateway(
                $"Upstream service timed out after {_options.TimeoutSeconds}s.",
                new { method = method.Method, uri });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Upstream FastAPI call failed: {Method} {Uri}", method, uri);
            return ProxyResult.BadGateway(
                $"Cannot reach upstream FastAPI service: {ex.Message}",
                new { method = method.Method, uri });
        }

        try
        {
            return await ParseAsync(resp, ct).ConfigureAwait(false);
        }
        finally
        {
            resp.Dispose();
        }
    }

    private static async Task<ProxyResult> ParseAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var status = (int)resp.StatusCode;
        var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        ApiResult? envelope = null;
        if (raw.Length > 0)
        {
            try
            {
                envelope = JsonSerializer.Deserialize<ApiResult>(raw, JsonOptions);
            }
            catch (JsonException)
            {
                // The upstream returned a non-envelope payload (e.g. an
                // HTML error page from a reverse proxy). Wrap it.
            }
        }

        if (envelope is null)
        {
            envelope = new ApiResult
            {
                Code = status,
                Message = resp.ReasonPhrase ?? $"HTTP {status}",
                Data = raw.Length == 0
                    ? null
                    : JsonDocument.Parse(raw.Length == 0 ? "null" : raw,
                        new JsonDocumentOptions { AllowTrailingCommas = true }).RootElement.Clone(),
            };
        }
        else if (envelope.Code == 0 && (status < 200 || status >= 300))
        {
            // Envelope claims success but HTTP status disagrees; honour
            // the HTTP status so callers see the failure.
            envelope.Code = status;
        }

        return new ProxyResult { StatusCode = status, Body = envelope };
    }

    private static string BuildQuery(IEnumerable<KeyValuePair<string, string?>>? query)
    {
        if (query is null) return string.Empty;
        var sb = new StringBuilder();
        var first = true;
        foreach (var kv in query)
        {
            if (string.IsNullOrEmpty(kv.Value)) continue;
            if (!first) sb.Append('&');
            first = false;
            sb.Append(Uri.EscapeDataString(kv.Key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(kv.Value));
        }
        return first ? string.Empty : "?" + sb;
    }
}

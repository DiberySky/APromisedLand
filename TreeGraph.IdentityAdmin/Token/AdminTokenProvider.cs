using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TreeGraph.IdentityAdmin.Options;

namespace TreeGraph.IdentityAdmin.Token;

/// <summary>
/// 基于 <c>client_credentials</c> 的管理令牌提供器。
/// 使用 <see cref="SemaphoreSlim"/> 单飞覆盖"首次获取"与"过期刷新"两条路径。
/// </summary>
public sealed class AdminTokenProvider : IAdminTokenProvider, IDisposable
{
    private static readonly Meter Meter = new("TreeGraph.IdentityAdmin", "1.0.0");
    private static readonly Counter<long> RefreshCounter =
        Meter.CreateCounter<long>("keycloak_admin_token_refresh_total");
    private static readonly Histogram<double> TokenAgeHistogram =
        Meter.CreateHistogram<double>("keycloak_token_age_seconds");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<KeycloakAdminOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AdminTokenProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt;

    public AdminTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<KeycloakAdminOptions> options,
        TimeProvider timeProvider,
        ILogger<AdminTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        if (_token is not null && _expiresAt - now > TimeSpan.FromSeconds(5))
        {
            TokenAgeHistogram.Record((now - (_expiresAt - TimeSpan.FromSeconds(60))).TotalSeconds);
            return _token;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            now = _timeProvider.GetUtcNow();
            if (_token is not null && _expiresAt - now > TimeSpan.FromSeconds(5))
                return _token;

            await RefreshCoreAsync(now, ct).ConfigureAwait(false);
            return _token!;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate()
    {
        _token = null;
        _expiresAt = DateTimeOffset.MinValue;
    }

    private async Task RefreshCoreAsync(DateTimeOffset now, CancellationToken ct)
    {
        var options = _options.CurrentValue;
        var tokenEndpoint = string.Concat(
            options.AdminBaseUrl.TrimEnd('/'),
            "/realms/",
            options.Realm,
            "/protocol/openid-connect/token");

        try
        {
            var client = _httpClientFactory.CreateClient(KeycloakClientNames.Token);
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = options.AdminClientId,
                ["client_secret"] = options.AdminClientSecret
            });

            using var response = await client.PostAsync(tokenEndpoint, content, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                RefreshCounter.Add(1, new KeyValuePair<string, object?>("result", "failed"));
                _logger.LogError(
                    "获取 Keycloak 管理令牌失败：HTTP {Status}，client={ClientId}",
                    (int)response.StatusCode,
                    options.AdminClientId);
                throw new KeycloakUnauthorizedException(
                    $"获取管理令牌失败：HTTP {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = document.RootElement;

            if (!root.TryGetProperty("access_token", out var tokenElement)
                || string.IsNullOrWhiteSpace(tokenElement.GetString()))
            {
                RefreshCounter.Add(1, new KeyValuePair<string, object?>("result", "empty"));
                throw new KeycloakUnauthorizedException("管理令牌响应缺少 access_token");
            }

            var expiresIn = root.TryGetProperty("expires_in", out var expiresElement)
                && expiresElement.TryGetInt32(out var seconds)
                    ? seconds
                    : 60;

            _token = tokenElement.GetString();
            _expiresAt = now.AddSeconds(Math.Max(10, expiresIn - 60));
            RefreshCounter.Add(1, new KeyValuePair<string, object?>("result", "success"));
            _logger.LogInformation(
                "Keycloak 管理令牌已刷新：client={ClientId}，有效期约 {ExpiresIn}s",
                options.AdminClientId,
                expiresIn);
        }
        catch (Exception ex) when (ex is not KeycloakUnauthorizedException and not OperationCanceledException)
        {
            RefreshCounter.Add(1, new KeyValuePair<string, object?>("result", "failed"));
            _logger.LogError(ex, "获取 Keycloak 管理令牌异常");
            throw new KeycloakUnauthorizedException("获取管理令牌异常：" + ex.Message);
        }
    }

    public void Dispose() => _gate.Dispose();
}

/// <summary>命名 HttpClient 常量。</summary>
public static class KeycloakClientNames
{
    public const string Token = "keycloak-token";
    public const string Admin = "keycloak-admin";
}

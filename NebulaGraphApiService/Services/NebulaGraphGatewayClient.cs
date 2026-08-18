using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NebulaGraphApiService.Services;

public class NebulaGraphGatewayClient : INebulaGraphClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly NebulaGraphGatewayOptions _options;
    private readonly ILogger<NebulaGraphGatewayClient> _logger;
    private readonly CookieContainer _cookieContainer;
    private bool _disposed;
    private bool _connected;

    public NebulaGraphGatewayClient(
        IOptions<NebulaGraphGatewayOptions> options,
        ILogger<NebulaGraphGatewayClient> logger)
    {
        _options = options.Value;
        _logger = logger;

        _cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri($"http://{_options.GatewayHost}:{_options.GatewayPort}")
        };
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    /// <summary>
    /// 连接到 Gateway，获取 Session Cookie（nsid）
    /// </summary>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connected) return;

        var request = new
        {
            username = _options.Username,
            password = _options.Password,
            address = _options.Host,      // graphd 地址（容器内名或 localhost）
            port = _options.Port            // 9669
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/api/db/connect", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(body);

        var code = doc.RootElement.GetProperty("code").GetInt32();
        if (code != 0)
        {
            var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "Unknown";
            throw new Exception($"Gateway connect failed: {msg}");
        }

        _connected = true;
        _logger.LogInformation("Connected to NebulaGraph via HTTP Gateway");
    }

    private async Task<JsonDocument> ExecuteQueryAsync(string ngql, CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);

        var request = new { gql = ngql };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/api/db/exec", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(body);

        var code = doc.RootElement.GetProperty("code").GetInt32();
        if (code != 0)
        {
            var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "Unknown";
            throw new Exception($"Gateway exec error: {msg}");
        }

        return doc;
    }

    // ---------- 接口实现 ----------

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureConnectedAsync(cancellationToken);
            await ExecuteQueryAsync("SHOW SPACES", cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test connection failed");
            return false;
        }
    }

    public async Task<bool> SpaceExistsAsync(string spaceName, CancellationToken cancellationToken = default)
    {
        var doc = await ExecuteQueryAsync("SHOW SPACES", cancellationToken);
        var data = doc.RootElement.GetProperty("data");
        var tables = data.GetProperty("tables");

        foreach (var item in tables.EnumerateArray())
        {
            // Gateway 返回的是 { "Name": "spaceName" } 格式
            foreach (var prop in item.EnumerateObject())
            {
                if (prop.Value.GetString() == spaceName)
                    return true;
            }
        }
        return false;
    }

    public async Task CreateSpaceAsync(string spaceName, CancellationToken cancellationToken = default)
    {
        var ngql = $"CREATE SPACE IF NOT EXISTS {spaceName} (vid_type=FIXED_STRING(32))";
        await ExecuteQueryAsync(ngql, cancellationToken);
        _logger.LogInformation("Space {SpaceName} created.", spaceName);
    }

    public async Task<bool> SpaceReadyAsync(string spaceName, CancellationToken cancellationToken = default)
    {
        try
        {
            await UseSpaceAsync(spaceName, cancellationToken);
            await ExecuteQueryAsync("SHOW TAGS", cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task UseSpaceAsync(string spaceName, CancellationToken cancellationToken = default)
    {
        await ExecuteQueryAsync($"USE {spaceName}", cancellationToken);
        _logger.LogDebug("Switched to space {SpaceName}.", spaceName);
    }

    public async Task<bool> TagExistsAsync(string tagName, CancellationToken cancellationToken = default)
    {
        var doc = await ExecuteQueryAsync("SHOW TAGS", cancellationToken);
        var data = doc.RootElement.GetProperty("data");
        var tables = data.GetProperty("tables");

        foreach (var item in tables.EnumerateArray())
        {
            foreach (var prop in item.EnumerateObject())
            {
                if (prop.Value.GetString() == tagName)
                    return true;
            }
        }
        return false;
    }

    public async Task<bool> EdgeExistsAsync(string edgeName, CancellationToken cancellationToken = default)
    {
        var doc = await ExecuteQueryAsync("SHOW EDGES", cancellationToken);
        var data = doc.RootElement.GetProperty("data");
        var tables = data.GetProperty("tables");

        foreach (var item in tables.EnumerateArray())
        {
            foreach (var prop in item.EnumerateObject())
            {
                if (prop.Value.GetString() == edgeName)
                    return true;
            }
        }
        return false;
    }

    public async Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken = default)
    {
        // 同时检查 TAG INDEXES 和 EDGE INDEXES
        foreach (var showCmd in new[] { "SHOW TAG INDEXES", "SHOW EDGE INDEXES" })
        {
            var doc = await ExecuteQueryAsync(showCmd, cancellationToken);
            var data = doc.RootElement.GetProperty("data");
            var tables = data.GetProperty("tables");

            foreach (var item in tables.EnumerateArray())
            {
                foreach (var prop in item.EnumerateObject())
                {
                    if (prop.Value.GetString() == indexName)
                        return true;
                }
            }
        }
        return false;
    }

    public async Task<bool> VertexExistsAsync(string vid, CancellationToken cancellationToken = default)
    {
        var ngql = $"FETCH PROP ON * {vid}";
        var doc = await ExecuteQueryAsync(ngql, cancellationToken);
        var data = doc.RootElement.GetProperty("data");
        var tables = data.GetProperty("tables");
        return tables.GetArrayLength() > 0;
    }

    public async Task<bool> EdgeExistsBetweenAsync(string src, string dst, string edgeName, CancellationToken cancellationToken = default)
    {
        var ngql = $"FETCH PROP ON {edgeName} {src} -> {dst}";
        var doc = await ExecuteQueryAsync(ngql, cancellationToken);
        var data = doc.RootElement.GetProperty("data");
        var tables = data.GetProperty("tables");
        return tables.GetArrayLength() > 0;
    }

    public async Task ExecuteAsync(string statement, CancellationToken cancellationToken = default)
    {
        await ExecuteQueryAsync(statement, cancellationToken);
        _logger.LogTrace("Executed: {Statement}", statement);
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            // 尝试断开连接
            _httpClient.PostAsync("/api/db/disconnect", new StringContent("")).Wait(TimeSpan.FromSeconds(2));
        }
        catch { /* ignore */ }

        _httpClient.Dispose();
        _disposed = true;
    }
}
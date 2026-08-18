// NebulaGraphHttpClient.cs
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NebulaGraphApiService.Services;

public class NebulaGraphHttpClient : INebulaGraphClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly NebulaGraphGatewayOptions _options;
    private readonly ILogger<NebulaGraphHttpClient> _logger;
    private bool _disposed;

    public NebulaGraphHttpClient(
        IOptions<NebulaGraphGatewayOptions> options,
        ILogger<NebulaGraphHttpClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://{_options.Host}:{_options.HttpPort}")
        };
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    private async Task<JsonDocument> ExecuteQueryAsync(string ngql, CancellationToken cancellationToken)
    {
        var request = new { stmt = ngql, @params = new { } };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/db", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(responseBody);

        // 检查错误码
        var root = doc.RootElement;
        if (root.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
        {
            var errMsg = errors[0].GetProperty("message").GetString();
            throw new Exception($"Nebula HTTP error: {errMsg}");
        }

        return doc;
    }

    private async Task<bool> ExecuteScalarAsync(string ngql, CancellationToken cancellationToken)
    {
        var doc = await ExecuteQueryAsync(ngql, cancellationToken);
        // 判断是否有数据行
        var results = doc.RootElement.GetProperty("results");
        if (results.GetArrayLength() == 0) return false;
        var data = results[0].GetProperty("data");
        return data.GetArrayLength() > 0;
    }

    // ---------- 接口实现 ----------

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await ExecuteQueryAsync("SHOW SPACES", cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SpaceExistsAsync(string spaceName, CancellationToken cancellationToken = default)
    {
        var doc = await ExecuteQueryAsync("SHOW SPACES", cancellationToken);
        var rows = doc.RootElement
            .GetProperty("results")[0]
            .GetProperty("data");
        foreach (var item in rows.EnumerateArray())
        {
            var row = item.GetProperty("row");
            if (row.GetArrayLength() > 0 && row[0].GetString() == spaceName)
                return true;
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
        var rows = doc.RootElement.GetProperty("results")[0].GetProperty("data");
        foreach (var item in rows.EnumerateArray())
        {
            var row = item.GetProperty("row");
            if (row.GetArrayLength() > 0 && row[0].GetString() == tagName)
                return true;
        }
        return false;
    }

    public async Task<bool> EdgeExistsAsync(string edgeName, CancellationToken cancellationToken = default)
    {
        var doc = await ExecuteQueryAsync("SHOW EDGES", cancellationToken);
        var rows = doc.RootElement.GetProperty("results")[0].GetProperty("data");
        foreach (var item in rows.EnumerateArray())
        {
            var row = item.GetProperty("row");
            if (row.GetArrayLength() > 0 && row[0].GetString() == edgeName)
                return true;
        }
        return false;
    }

    public async Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken = default)
    {
        var doc = await ExecuteQueryAsync("SHOW TAG INDEXES", cancellationToken);
        var rows = doc.RootElement.GetProperty("results")[0].GetProperty("data");
        foreach (var item in rows.EnumerateArray())
        {
            var row = item.GetProperty("row");
            if (row.GetArrayLength() > 0 && row[0].GetString() == indexName)
                return true;
        }
        return false;
    }

    public async Task<bool> VertexExistsAsync(string vid, CancellationToken cancellationToken = default)
    {
        var ngql = $"FETCH PROP ON * {vid}";
        var doc = await ExecuteQueryAsync(ngql, cancellationToken);
        var rows = doc.RootElement.GetProperty("results")[0].GetProperty("data");
        return rows.GetArrayLength() > 0;
    }

    public async Task<bool> EdgeExistsBetweenAsync(string src, string dst, string edgeName, CancellationToken cancellationToken = default)
    {
        var ngql = $"FETCH PROP ON {edgeName} {src} -> {dst}";
        var doc = await ExecuteQueryAsync(ngql, cancellationToken);
        var rows = doc.RootElement.GetProperty("results")[0].GetProperty("data");
        return rows.GetArrayLength() > 0;
    }

    public async Task ExecuteAsync(string statement, CancellationToken cancellationToken = default)
    {
        await ExecuteQueryAsync(statement, cancellationToken);
        _logger.LogTrace("Executed: {Statement}", statement);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
    }
}
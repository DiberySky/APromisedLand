using System.Text.Json;
using MAFWorkFlowApi.Agents;
using MAFWorkFlowApi.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MAFWorkFlowApi.HealthChecks;

/// <summary>
/// 就绪检查：验证 Ollama 服务可达，且目标模型已实际拉取到本地。
/// 通过 IHttpClientFactory 创建命名客户端，模型名精确匹配，结果缓存 30/5 秒。
/// </summary>
public sealed class OllamaModelReadyHealthCheck : IHealthCheck
{
    /// <summary>健康检查专用 HttpClient 名称（在 Program.cs 注册）。</summary>
    public const string HttpClientName = "ollama-health";

    private static readonly MemoryCache Cache = new(new MemoryCacheOptions
    {
        SizeLimit = 16,
    });

    private static readonly TimeSpan HealthyTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UnhealthyTtl = TimeSpan.FromSeconds(5);

    private readonly OllamaAgentOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OllamaModelReadyHealthCheck> _logger;

    public OllamaModelReadyHealthCheck(
        IOptions<OllamaAgentOptions> options,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OllamaModelReadyHealthCheck> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var endpoint = OllamaEndpointResolver.Resolve(_configuration, _options.Endpoint);
        var modelId = _options.ModelId;
        var cacheKey = $"{endpoint}|{modelId}";

        if (Cache.Get(cacheKey) is HealthCheckResult cached)
        {
            _logger.LogDebug(
                "Health check cache hit for {Model} @ {Endpoint}, status = {Status}",
                modelId, endpoint, cached.Status);
            return cached;
        }

        var result = await DoCheckAsync(endpoint, modelId, cancellationToken);

        var ttl = result.Status == HealthStatus.Healthy ? HealthyTtl : UnhealthyTtl;
        Cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = 1,
        });

        return result;
    }

    private async Task<HealthCheckResult> DoCheckAsync(
        string endpoint, string modelId, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate($"{endpoint}/api/tags", UriKind.Absolute, out var tagsUri))
        {
            return HealthCheckResult.Unhealthy(
                $"Ollama endpoint 配置无效：'{endpoint}'",
                data: new Dictionary<string, object>
                {
                    ["resolvedEndpoint"] = endpoint,
                    ["modelId"] = modelId
                });
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            using var response = await client.GetAsync(
                tagsUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Unhealthy(
                    $"Ollama /api/tags 返回 {(int)response.StatusCode}",
                    data: new Dictionary<string, object>
                    {
                        ["endpoint"] = endpoint,
                        ["statusCode"] = (int)response.StatusCode
                    });
            }

            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken);

            var installedModels = await ParseInstalledModelsAsync(
                stream, cancellationToken);

            if (!IsModelInstalled(installedModels, modelId))
            {
                _logger.LogWarning(
                    "Ollama 可达但目标模型 {Model} 尚未拉取。已安装：{Models}",
                    modelId, string.Join(", ", installedModels));

                return HealthCheckResult.Unhealthy(
                    $"模型 {modelId} 尚未就绪",
                    data: new Dictionary<string, object>
                    {
                        ["endpoint"] = endpoint,
                        ["targetModel"] = modelId,
                        ["installedModels"] = installedModels
                    });
            }

            return HealthCheckResult.Healthy(
                $"Ollama 可达，模型 {modelId} 已就绪",
                data: new Dictionary<string, object>
                {
                    ["endpoint"] = endpoint,
                    ["targetModel"] = modelId,
                    ["installedModelCount"] = installedModels.Count
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "无法连接 Ollama：{Endpoint}", endpoint);
            return HealthCheckResult.Unhealthy(
                $"无法连接 Ollama：{ex.Message}",
                data: new Dictionary<string, object>
                {
                    ["endpoint"] = endpoint,
                    ["exceptionType"] = ex.GetType().Name
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama 健康检查异常");
            return HealthCheckResult.Unhealthy(
                $"Ollama 健康检查异常：{ex.Message}",
                data: new Dictionary<string, object>
                {
                    ["exceptionType"] = ex.GetType().Name
                });
        }
    }

    private static async Task<List<string>> ParseInstalledModelsAsync(
        Stream stream, CancellationToken cancellationToken)
    {
        var result = new List<string>();
        try
        {
            using var doc = await JsonDocument.ParseAsync(
                stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("models", out var modelsEl) ||
                modelsEl.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var m in modelsEl.EnumerateArray())
            {
                if (m.TryGetProperty("name", out var nameEl) &&
                    nameEl.ValueKind == JsonValueKind.String)
                {
                    var name = nameEl.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        result.Add(name);
                }
            }
        }
        catch (JsonException)
        {
            // 结构异常时返回空列表，由调用方按「未就绪」处理
        }

        return result;
    }

    /// <summary>
    /// 模型名精确匹配；若目标未指定标签，则额外允许匹配 "{target}:latest"。
    /// </summary>
    private static bool IsModelInstalled(
        IReadOnlyList<string> installed, string target)
    {
        if (installed.Count == 0 || string.IsNullOrWhiteSpace(target))
            return false;

        foreach (var name in installed)
        {
            if (string.Equals(name, target, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (!target.Contains(':'))
        {
            var withLatest = $"{target}:latest";
            foreach (var name in installed)
            {
                if (string.Equals(name, withLatest, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
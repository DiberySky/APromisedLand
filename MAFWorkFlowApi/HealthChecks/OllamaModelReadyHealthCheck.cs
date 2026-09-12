using System.Data.Common;
using System.Text.Json;
using MAFWorkFlowApi.Agents;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MAFWorkFlowApi.HealthChecks;

/// <summary>
/// 就绪检查：验证 Ollama 服务可达，且目标模型已实际拉取到本地。
///
/// 设计要点：
///   1. 静态 HttpClient —— 绕过 Aspire ServiceDefaults 注入的
///      ServiceDiscovery / Polly / OTel 管道（节省约 120ms）。
///   2. 内存缓存 —— Ollama /api/tags 在 Docker Desktop 环境下响应
///      约 2 秒，通过 30 秒缓存将后续检查的耗时降至亚毫秒。
///   3. 差异化 TTL —— Healthy 缓存 30s；Unhealthy 缓存 5s，
///      保证模型拉取完成后能快速切换为 Healthy。
/// </summary>
public sealed class OllamaModelReadyHealthCheck : IHealthCheck
{
    // ─── 共享 HttpClient ────────────────────────────────────────────
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)   // 容忍 Ollama 慢响应
    };

    // ─── 共享缓存 ───────────────────────────────────────────────────
    // 使用 MemoryCache 具体类 + Get/Set 方法，避免泛型扩展方法的
    // 类型推断歧义（MemoryCache 自身的 TryGetValue 是非泛型的）。
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions
    {
        SizeLimit = 16,
    });

    // ─── 缓存 TTL 常量 ──────────────────────────────────────────────
    private static readonly TimeSpan HealthyTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UnhealthyTtl = TimeSpan.FromSeconds(5);

    private readonly OllamaAgentOptions _options;
    private readonly ILogger<OllamaModelReadyHealthCheck> _logger;

    public OllamaModelReadyHealthCheck(
        IOptions<OllamaAgentOptions> options,
        ILogger<OllamaModelReadyHealthCheck> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var endpoint = ResolveEndpoint(_options);
        var modelId = _options.ModelId;

        // ─── 缓存键：endpoint 或模型变化时自动失效 ───────────────────
        var cacheKey = $"{endpoint}|{modelId}";

        // ⭐ 使用 Get + is 模式匹配：类型明确，无推断歧义
        if (Cache.Get(cacheKey) is HealthCheckResult cached)
        {
            _logger.LogDebug(
                "Health check cache hit for {Model} @ {Endpoint}, status = {Status}",
                modelId, endpoint, cached.Status);

            return cached;
        }

        // ─── 缓存未命中：真正查询 Ollama ─────────────────────────────
        var result = await DoCheckAsync(endpoint, modelId, cancellationToken);

        // ─── 差异化 TTL ─────────────────────────────────────────────
        var ttl = result.Status == HealthStatus.Healthy ? HealthyTtl : UnhealthyTtl;

        Cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = 1,
        });

        return result;
    }

    private async Task<HealthCheckResult> DoCheckAsync(
        string endpoint,
        string modelId,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate($"{endpoint}/api/tags", UriKind.Absolute, out var tagsUri))
        {
            _logger.LogWarning(
                "Ollama endpoint 解析失败，非绝对 URI: '{Endpoint}'", endpoint);

            return HealthCheckResult.Unhealthy(
                $"Ollama endpoint 配置无效: '{endpoint}'",
                data: new Dictionary<string, object>
                {
                    ["resolvedEndpoint"] = endpoint,
                    ["modelId"] = modelId,
                    ["hint"] = "检查 ConnectionStrings__chat-model / services__* 是否注入"
                });
        }

        try
        {
            using var response = await Http.GetAsync(
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
            using var doc = await JsonDocument.ParseAsync(
                stream, cancellationToken: cancellationToken);

            var installedModels = doc.RootElement
                .GetProperty("models")
                .EnumerateArray()
                .Select(m => m.GetProperty("name").GetString())
                .Where(n => n is not null)
                .ToList();

            var isModelReady = installedModels.Any(m =>
                m!.StartsWith(modelId, StringComparison.OrdinalIgnoreCase));

            if (!isModelReady)
            {
                _logger.LogWarning(
                    "Ollama 可达但目标模型 {Model} 尚未拉取完成。已安装模型：{Models}",
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
            _logger.LogWarning(ex, "无法连接 Ollama: {Endpoint}", endpoint);
            return HealthCheckResult.Unhealthy(
                $"无法连接 Ollama: {ex.Message}",
                exception: ex,
                data: new Dictionary<string, object> { ["endpoint"] = endpoint });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama 健康检查异常");
            return HealthCheckResult.Unhealthy(
                $"Ollama 健康检查异常: {ex.Message}",
                exception: ex);
        }
    }

    private static string ResolveEndpoint(OllamaAgentOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            var fromOptions = NormalizeEndpoint(options.Endpoint);
            if (fromOptions is not null)
                return fromOptions;
        }

        string?[] candidates =
        [
            Environment.GetEnvironmentVariable("ConnectionStrings__chat-model"),
            Environment.GetEnvironmentVariable("ConnectionStrings__ollama"),
            Environment.GetEnvironmentVariable("services__chat-model__http__0"),
            Environment.GetEnvironmentVariable("services__ollama__http__0"),
        ];

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var normalized = NormalizeEndpoint(candidate);
            if (normalized is not null)
                return normalized;
        }

        return "http://localhost:11434";
    }

    private static string? NormalizeEndpoint(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
            return null;

        if (trimmed.Contains('='))
        {
            var extracted = TryExtractEndpointFromConnectionString(trimmed);
            return extracted is not null ? AddSchemeIfMissing(extracted) : null;
        }

        return AddSchemeIfMissing(trimmed);
    }

    private static string? TryExtractEndpointFromConnectionString(string connectionString)
    {
        try
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (builder.TryGetValue("Endpoint", out var value)
                && value is string endpointStr
                && !string.IsNullOrWhiteSpace(endpointStr))
            {
                return endpointStr.Trim();
            }
        }
        catch (ArgumentException)
        {
            // 连接字符串语法非法，静默失败交由上层处理
        }

        return null;
    }

    private static string AddSchemeIfMissing(string url)
    {
        var trimmed = url.Trim().TrimEnd('/');

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return "http://" + trimmed;
    }
}
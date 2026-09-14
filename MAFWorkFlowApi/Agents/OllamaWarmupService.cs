using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 应用启动后异步预热 Ollama 模型，减少首次用户请求的冷启动延迟。
/// 预热失败不影响应用启动，仅记录日志。
/// </summary>
public sealed class OllamaWarmupService : IHostedService
{
    /// <summary>预热专用 HttpClient 名称（在 Program.cs 注册）。</summary>
    public const string HttpClientName = "ollama-warmup";

    private const string KeepAliveDuration = "30m";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OllamaAgentOptions _options;
    private readonly ILogger<OllamaWarmupService> _logger;

    public OllamaWarmupService(
        IHttpClientFactory httpClientFactory,
        IOptions<OllamaAgentOptions> options,
        ILogger<OllamaWarmupService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // fire-and-forget：不阻塞应用启动
        _ = Task.Run(() => WarmupAsync(cancellationToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task WarmupAsync(CancellationToken ct)
    {
        var endpoint = ResolveEndpoint();
        var modelId = _options.ModelId;

        _logger.LogInformation(
            "开始预热 Ollama 模型：{Model} @ {Endpoint}", modelId, endpoint);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            // /api/generate 空 prompt 触发模型加载，keep_alive 让模型常驻内存
            var payload = new
            {
                model = modelId,
                prompt = string.Empty,
                keep_alive = KeepAliveDuration,
            };

            using var response = await client.PostAsJsonAsync(
                $"{endpoint}/api/generate", payload, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Ollama 模型预热成功：{Model}，keep_alive = {KeepAlive}",
                    modelId, KeepAliveDuration);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Ollama 预热返回非成功状态：{StatusCode}，响应：{Body}",
                    response.StatusCode, body);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 应用关闭时取消，忽略
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Ollama 预热失败（不影响应用启动），模型：{Model}", modelId);
        }
    }

    private string ResolveEndpoint()
    {
        // 与健康检查相同的端点解析逻辑
        string?[] candidates =
        [
            _options.Endpoint,
            Environment.GetEnvironmentVariable("ConnectionStrings__chat-model"),
            Environment.GetEnvironmentVariable("ConnectionStrings__ollama"),
            Environment.GetEnvironmentVariable("services__chat-model__http__0"),
            Environment.GetEnvironmentVariable("services__ollama__http__0"),
        ];

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;

            var trimmed = candidate.Trim().TrimEnd('/');

            // 处理连接字符串格式（含 "="）
            if (trimmed.Contains('='))
            {
                var extracted = TryExtractEndpointFromConnectionString(trimmed);
                if (extracted is null) continue;
                trimmed = extracted;
            }

            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                trimmed = "http://" + trimmed;

            return trimmed;
        }

        return "http://localhost:11434";
    }

    private static string? TryExtractEndpointFromConnectionString(string connectionString)
    {
        try
        {
            var builder = new System.Data.Common.DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };
            return builder.TryGetValue("Endpoint", out var value)
                && value is string s
                && !string.IsNullOrWhiteSpace(s)
                ? s.Trim()
                : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
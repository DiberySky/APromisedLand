using System.Net.Http.Json;
using MAFWorkFlowApi.Infrastructure;
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
    private readonly IConfiguration _configuration;
    private readonly ILogger<OllamaWarmupService> _logger;

    public OllamaWarmupService(
        IHttpClientFactory httpClientFactory,
        IOptions<OllamaAgentOptions> options,
        IConfiguration configuration,
        ILogger<OllamaWarmupService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _configuration = configuration;
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
        var endpoint = OllamaEndpointResolver.Resolve(_configuration, _options.Endpoint);
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
}
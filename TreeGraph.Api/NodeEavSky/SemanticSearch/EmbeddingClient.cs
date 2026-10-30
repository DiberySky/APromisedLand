using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>
/// vLLM OpenAI 兼容端点的向量化客户端。
/// 端点：{EmbeddingEndpoint}/embeddings（bge-m3，维度 1024）。
///
/// 失败短路：embedding 服务不可用时，首次失败后在冷却窗口内
/// （<see cref="SemanticSearchOptions.EmbeddingFailureCooldownSeconds"/>）
/// 所有调用直接抛 <see cref="EmbeddingUnavailableException"/>，不再发起 HTTP ——
/// 避免每次向量同步都在 HTTP 超时上等待、连坐同消息里后执行的图同步 handler。
/// </summary>
public sealed class EmbeddingClient
{
    private readonly HttpClient _http;
    private readonly SemanticSearchOptions _options;
    private readonly ILogger<EmbeddingClient> _logger;

    // 熔断状态：静态共享（typed HttpClient 客户端实例为 transient，但短路必须跨实例生效）。
    // 值 = 熔断结束的 UTC ticks；0 表示闭合（可请求）。
    private static long _circuitOpenUntilTicks;

    public EmbeddingClient(
        HttpClient http,
        IOptions<SemanticSearchOptions> options,
        ILogger<EmbeddingClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>当前是否处于熔断冷却（短路中）。</summary>
    public static bool IsCircuitOpen
        => DateTime.UtcNow < new DateTime(
            Interlocked.Read(ref _circuitOpenUntilTicks), DateTimeKind.Utc);

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        // ★ 防御：空文本不向量化——零向量会污染索引（与一切"不相似但可被搜出"）
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("向量化文本不能为空", nameof(text));

        // 熔断期：直接失败，不发起 HTTP。
        if (IsCircuitOpen)
            throw new EmbeddingUnavailableException(
                $"embedding 服务处于失败冷却（{_options.EmbeddingFailureCooldownSeconds}s），跳过向量化");

        var request = new
        {
            model = _options.EmbeddingModel,
            input = new[] { text }
        };

        try
        {
            var response = await _http.PostAsJsonAsync("embeddings", request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<OpenAiEmbeddingResponse>(ct);

            var vector = result?.Data?.FirstOrDefault()?.Embedding;
            if (vector is null || vector.Length != _options.VectorDimension)
            {
                throw new InvalidOperationException(
                    $"向量维度异常：期望 {_options.VectorDimension}，" +
                    $"实际 {vector?.Length ?? 0}");
            }

            // 成功：闭合熔断。
            Interlocked.Exchange(ref _circuitOpenUntilTicks, 0);
            _logger.LogDebug("Embedding 生成成功：dim={Dim}", vector.Length);
            return vector;
        }
        catch (OperationCanceledException)
        {
            // 取消（如宿主关停）不触发熔断，直接抛出。
            throw;
        }
        catch (Exception ex)
        {
            // 打开熔断：冷却窗口内后续调用全部短路。
            if (_options.EmbeddingFailureCooldownSeconds > 0)
            {
                var until = DateTime.UtcNow
                    .AddSeconds(_options.EmbeddingFailureCooldownSeconds).Ticks;
                Interlocked.Exchange(ref _circuitOpenUntilTicks, until);
                _logger.LogWarning(
                    ex,
                    "Embedding 请求失败，开启失败冷却 {Cooldown}s（{Until:o}）",
                    _options.EmbeddingFailureCooldownSeconds,
                    new DateTime(until, DateTimeKind.Utc));
            }
            throw;
        }
    }

    private sealed class OpenAiEmbeddingResponse
    {
        public List<EmbeddingData>? Data { get; set; }
    }

    private sealed class EmbeddingData
    {
        public float[]? Embedding { get; set; }
    }
}

/// <summary>embedding 服务处于失败冷却期（熔断中），调用方应降级跳过。</summary>
public sealed class EmbeddingUnavailableException : Exception
{
    public EmbeddingUnavailableException(string message) : base(message) { }
}

using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>
/// vLLM OpenAI 兼容端点的向量化客户端。
/// 端点：{EmbeddingEndpoint}/embeddings（bge-m3，维度 1024）。
/// </summary>
public sealed class EmbeddingClient
{
    private readonly HttpClient _http;
    private readonly SemanticSearchOptions _options;
    private readonly ILogger<EmbeddingClient> _logger;

    public EmbeddingClient(
        HttpClient http,
        IOptions<SemanticSearchOptions> options,
        ILogger<EmbeddingClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        // ★ 防御：空文本不向量化——零向量会污染索引（与一切"不相似但可被搜出"）
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("向量化文本不能为空", nameof(text));

        var request = new
        {
            model = _options.EmbeddingModel,
            input = new[] { text }
        };

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

        _logger.LogDebug("Embedding 生成成功：dim={Dim}", vector.Length);
        return vector;
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

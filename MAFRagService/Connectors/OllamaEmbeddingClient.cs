using System.Text.Json;
using MAFRagService.Startup.Configuration;
using Microsoft.Extensions.Options;

namespace MAFRagService.Connectors;

/// <summary>
/// 调用 Ollama /api/embeddings。
/// 模型名从 OllamaOptions 读，不直接读 IConfiguration。
/// HttpClient 由调用方通过 HttpClientNames.Ollama 创建后注入。
/// </summary>
public sealed class OllamaEmbeddingClient : IOllamaEmbeddingClient
{
    private readonly HttpClient _http;
    private readonly IOptions<OllamaOptions> _options;

    public OllamaEmbeddingClient(
        HttpClient http,
        IOptions<OllamaOptions> options)
    {
        _http    = http;
        _options = options;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<float>();

        var model   = _options.Value.EmbeddingModel;
        var payload = new { model, prompt = text };

        using var resp = await _http.PostAsJsonAsync("/api/embeddings", payload, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Ollama embeddings 失败: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("embedding", out var arr))
            throw new InvalidOperationException("Ollama embeddings 响应缺少 embedding 字段。");

        var result = new float[arr.GetArrayLength()];
        var i = 0;
        foreach (var v in arr.EnumerateArray()) result[i++] = (float)v.GetDouble();
        return result;
    }
}
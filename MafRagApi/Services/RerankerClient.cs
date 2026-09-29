using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MafRagApi.Models;
using Microsoft.Extensions.Options;

namespace MafRagApi.Services;

/// <summary>
/// 基于 HttpClient 的 Reranker 客户端。
/// 上游契约：POST /rerank，body {query, documents}，
/// 返回 {results:[{index,relevance_score}], model, device}。
/// 上游不认 top_n，截断客户端做。
/// </summary>
public sealed class RerankerClient(
    HttpClient http,
    IOptions<RerankerOptions> options,
    ILogger<RerankerClient> logger) : IRerankerClient
{
    private readonly RerankerOptions _options = options.Value;

    public async Task<IReadOnlyList<RerankResultDto>> RerankAsync(
        string query,
        IReadOnlyList<string> documents,
        int? topK = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("query is required.", nameof(query));

        if (documents is null || documents.Count == 0)
            return Array.Empty<RerankResultDto>();

        if (query.Length > 500)
            throw new ArgumentException(
                $"query 长度 {query.Length} 超过上游上限 500。", nameof(query));

        if (documents.Count > _options.MaxDocuments)
            throw new ArgumentException(
                $"documents.Count={documents.Count} 超过 MaxDocuments={_options.MaxDocuments}。",
                nameof(documents));

        var k = topK ?? _options.DefaultTopK;
        if (k <= 0) k = _options.DefaultTopK;
        if (k > documents.Count) k = documents.Count;

        var req = new UpstreamRerankRequest { Query = query, Documents = documents };

        logger.LogDebug("Reranker: queryLen={QLen}, docs={Docs}, topK={TopK}",
            query.Length, documents.Count, k);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage resp;
        try
        {
            resp = await http.PostAsJsonAsync("rerank", req, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Reranker 不可达: {BaseAddress}", http.BaseAddress);
            throw;
        }
        sw.Stop();

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            logger.LogError("Reranker HTTP {StatusCode} ({Ms}ms): {Body}",
                (int)resp.StatusCode, sw.ElapsedMilliseconds, body);
            throw new HttpRequestException(
                $"Reranker returned {(int)resp.StatusCode}: {body}");
        }

        var upstream = await resp.Content.ReadFromJsonAsync<UpstreamRerankResponse>(ct);
        if (upstream?.Results is null || upstream.Results.Count == 0)
            return Array.Empty<RerankResultDto>();

        var results = upstream.Results
            .Where(r => r.Index >= 0 && r.Index < documents.Count)
            .Select(r => new RerankResultDto
            {
                Index    = r.Index,
                Score    = r.RelevanceScore,
                Document = documents[r.Index],
            })
            .OrderByDescending(r => r.Score)
            .Take(k)
            .ToArray();

        logger.LogInformation("Reranker done: {In} docs → {Out} results in {Ms}ms",
            documents.Count, results.Length, sw.ElapsedMilliseconds);

        return results;
    }

    // ─── 上游 DTO ─────────────────────────────────────────────
    private sealed class UpstreamRerankRequest
    {
        [JsonPropertyName("query")]     public string Query { get; set; } = "";
        [JsonPropertyName("documents")] public IReadOnlyList<string> Documents { get; set; } = Array.Empty<string>();
    }

    private sealed class UpstreamRerankResponse
    {
        [JsonPropertyName("results")] public List<UpstreamRerankItem>? Results { get; set; }
        [JsonPropertyName("model")]   public string? Model { get; set; }
        [JsonPropertyName("device")]  public string? Device { get; set; }
    }

    private sealed class UpstreamRerankItem
    {
        [JsonPropertyName("index")]           public int Index { get; set; }
        [JsonPropertyName("relevance_score")] public double RelevanceScore { get; set; }
    }
}
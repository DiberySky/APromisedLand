using System.Globalization;
using System.Text.Json;
using MAFRagService.Connectors;
using MAFRagService.Models;

namespace MAFRagService.Services;

/// <summary>
/// 向量检索服务。
///
/// <para><b>为什么用 nearVector 而不是 nearText：</b></para>
/// <list type="bullet">
///   <item>Weaviate 配置了 <c>DEFAULT_VECTORIZER_MODULE=none</c>，即客户端提供向量；</item>
///   <item><c>nearText</c> 需要 Weaviate 自己做向量化，本场景不可用（会静默返回空）；</item>
///   <item>因此先用 <see cref="IOllamaEmbeddingClient"/> 生成查询向量，
///         再用 <c>nearVector</c> 传给 Weaviate。</item>
/// </list>
///
/// <para><b>开发阶段约定：</b></para>
/// <list type="bullet">
///   <item>只按 tenant 过滤，不做 version 过滤
///         （上传时 version = "v1"，检索时默认 "latest"，直接过滤会匹配 0 条）。</item>
///   <item>URL 用相对路径 <c>/v1/graphql</c>，由命名 HttpClient 的 BaseAddress
///         自动拼接（BaseAddress 来自 ConnectionStrings__weaviate）。</item>
/// </list>
/// </summary>
public class RagService
{
    private const string HttpClientName = "WeaviateClient";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOllamaEmbeddingClient _embedding;
    private readonly ILogger<RagService> _logger;

    public RagService(
        IHttpClientFactory httpClientFactory,
        IOllamaEmbeddingClient embedding,
        ILogger<RagService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _embedding         = embedding;
        _logger            = logger;
    }

    public async Task<List<Source>> SearchAsync(
        string query,
        string tenant,
        int topK,
        string version,
        CancellationToken ct)
    {
        // ---------- 1. 生成查询向量 ----------
        float[] queryVector;
        try
        {
            queryVector = await _embedding.EmbedAsync(query, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成查询向量失败，跳过检索");
            return new List<Source>();
        }

        if (queryVector.Length == 0)
        {
            _logger.LogWarning("查询向量为空，跳过检索");
            return new List<Source>();
        }

        _logger.LogDebug("查询向量维度 = {Dim}", queryVector.Length);

        // ---------- 2. 构造 GraphQL（nearVector） ----------
        var vectorLiteral = "[" + string.Join(",",
            queryVector.Select(f => f.ToString("R", CultureInfo.InvariantCulture))) + "]";

        var graphql = new
        {
            query = $@"
            {{
                Get {{
                    Document(
                        nearVector: {{ vector: {vectorLiteral} }},
                        where: {{ path: [""tenant""], operator: Equal, valueString: ""{Escape(tenant)}"" }},
                        limit: {topK}
                    ) {{
                        docId
                        content
                        chunkIndex
                        version
                        _additional {{ distance }}
                    }}
                }}
            }}"
        };

        // ---------- 3. 发请求 ----------
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.PostAsJsonAsync("/v1/graphql", graphql, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Weaviate GraphQL 失败: {Status} {Body}",
                (int)response.StatusCode, body);
            return new List<Source>();
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("Weaviate response: {Json}", json);

        // ---------- 4. 解析响应 ----------
        var sources = new List<Source>();
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("errors", out var errors) &&
                errors.GetArrayLength() > 0)
            {
                _logger.LogError("Weaviate GraphQL errors: {Errors}", errors.ToString());
                return sources;
            }

            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("Get", out var get) ||
                !get.TryGetProperty("Document", out var docs))
            {
                _logger.LogWarning("Weaviate 响应缺少 data.Get.Document: {Json}", json);
                return sources;
            }

            foreach (var item in docs.EnumerateArray())
            {
                var src = new Source
                {
                    DocId   = item.TryGetProperty("docId",   out var d) ? d.GetString() ?? "" : "",
                    Content = item.TryGetProperty("content", out var c) ? c.GetString() ?? "" : ""
                };

                if (item.TryGetProperty("_additional", out var add) &&
                    add.TryGetProperty("distance", out var dist))
                {
                    src.Score = 1.0f - (float)dist.GetDouble();
                }

                sources.Add(src);
            }

            _logger.LogInformation(
                "Weaviate 检索命中 {Count} 条 (query='{Query}', tenant={Tenant})",
                sources.Count, Truncate(query, 40), tenant);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Weaviate response");
        }

        return sources;
    }

    private static string Escape(string s) =>
        (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "...";
}
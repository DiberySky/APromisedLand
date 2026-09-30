using System.Text;
using System.Text.Json.Nodes;
using APromisedLand.Api.MafRag.Dtos;
using Microsoft.Extensions.AI;

namespace MafVectorSearchApi.Services;

/// <summary>
/// 内存版 RAG 向量检索服务（不含 LLM 生成）。
///
/// 流水线：
///   Ingest  → 文本/JSON 分块 → embedding → 存入内存向量库
///   Retrieve → query embedding → 余弦相似度 → 可选 reranker 精排
///
/// RAG 问答编排（检索 + 拼装上下文 + LLM 生成）由调用方负责。
/// 生产环境请把 _chunks 换成 Qdrant / Milvus / pgvector 等向量库。
/// </summary>
public sealed class RagService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embedder;
    private readonly IRerankerClient _reranker;
    private readonly ILogger<RagService> _logger;

    private readonly List<RagChunk> _chunks = new();
    private readonly Dictionary<string, RagDoc> _documents = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RagService(
        IEmbeddingGenerator<string, Embedding<float>> embedder,
        IRerankerClient reranker,
        ILogger<RagService> logger)
    {
        _embedder = embedder;
        _reranker = reranker;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════
    // 1. 摄入
    // ═══════════════════════════════════════════════════════════

    public async Task<RagIngestResponse> IngestAsync(RagIngestRequest req, CancellationToken ct = default)
    {
        var docId = Guid.NewGuid().ToString("N");
        var title = string.IsNullOrWhiteSpace(req.Title) ? $"doc-{docId[..8]}" : req.Title;

        List<(string? Path, string Text)> chunks;
        string mode;

        // ── 尝试按 JSON 扁平化分块 ──
        if (TryParseJson(req.Content, out var jsonRoot))
        {
            chunks = FlattenJson(jsonRoot!)
                .Select(x => (x.Path, $"{x.Path}: {x.Value}"))
                .ToList();
            mode = "json";
        }
        else
        {
            // 普通文本：按 ChunkSize 滑窗分块
            chunks = SplitText(req.Content, req.ChunkSize)
                .Select(t => ((string?)null, t))
                .ToList();
            mode = "text";
        }

        if (chunks.Count == 0)
            return new RagIngestResponse { DocId = docId, Title = title, ChunkCount = 0, Mode = mode };

        var texts = chunks.Select(c => c.Text).ToList();

        // 批量 embedding（失败则降级为无向量，检索时走 BM25）
        float[][] vectors;
        try
        {
            var embeddings = await _embedder.GenerateAsync(texts, cancellationToken: ct);
            vectors = embeddings.Select(e => e.Vector.ToArray()).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding 不可用，摄入降级为无向量模式（检索将使用 BM25）");
            vectors = texts.Select(_ => Array.Empty<float>()).ToArray();
        }

        await _lock.WaitAsync(ct);
        try
        {
            _documents[docId] = new RagDoc { DocId = docId, Title = title, RawText = req.Content };
            for (int i = 0; i < chunks.Count; i++)
            {
                _chunks.Add(new RagChunk
                {
                    ChunkId = $"{docId}-{i}",
                    DocId   = docId,
                    Title   = title,
                    Path    = chunks[i].Path,
                    Content = chunks[i].Text,
                    Vector  = vectors[i],
                });
            }
        }
        finally { _lock.Release(); }

        _logger.LogInformation("RAG ingest: docId={DocId}, mode={Mode}, chunks={N}", docId, mode, chunks.Count);

        return new RagIngestResponse
        {
            DocId      = docId,
            Title      = title,
            ChunkCount = chunks.Count,
            Mode       = mode,
        };
    }

    // ═══════════════════════════════════════════════════════════
    // 2. 检索
    // ═══════════════════════════════════════════════════════════

    public async Task<RagRetrieveResponse> RetrieveAsync(RagRetrieveRequest req, CancellationToken ct = default)
    {
        List<RagChunk> snapshot;
        await _lock.WaitAsync(ct);
        try { snapshot = _chunks.ToList(); }
        finally { _lock.Release(); }

        if (snapshot.Count == 0)
            return new RagRetrieveResponse { Query = req.Query, TotalChunks = 0 };

        // 判断是否有可用向量
        var hasVectors = snapshot.All(c => c.Vector.Length > 0);

        float[]? qVec = null;
        if (hasVectors)
        {
            try
            {
                var qEmb = await _embedder.GenerateAsync(new[] { req.Query }, cancellationToken: ct);
                qVec = qEmb[0].Vector.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Query embedding 失败，降级为 BM25 检索");
                hasVectors = false;
            }
        }

        List<(RagChunk Chunk, double Score)> ranked;

        if (hasVectors && qVec is not null)
        {
            // ── 语义检索：余弦相似度 ──
            ranked = snapshot
                .Select(c => (Chunk: c, Score: Cosine(qVec!, c.Vector)))
                .OrderByDescending(x => x.Score)
                .Take(Math.Min(req.TopK * 3, snapshot.Count))
                .ToList();

            // 可选 reranker 精排
            if (req.UseReranker && ranked.Count > 1)
            {
                try
                {
                    var docs = ranked.Select(x => x.Chunk.Content).ToArray();
                    var reranked = await _reranker.RerankAsync(req.Query, docs, req.TopK, ct);
                    ranked = reranked
                        .Select(r => ranked[r.Index])
                        .ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Rerank 失败，保留向量粗排结果");
                    ranked = ranked.Take(req.TopK).ToList();
                }
            }
            else
            {
                ranked = ranked.Take(req.TopK).ToList();
            }
        }
        else
        {
            // ── 降级：BM25 关键词检索 ──
            ranked = Bm25Search(req.Query, snapshot, req.TopK);
        }

        var hits = ranked.Select(x => ToHit(x.Chunk, x.Score)).ToList();

        return new RagRetrieveResponse
        {
            Query       = req.Query,
            Hits        = hits,
            TotalChunks = snapshot.Count,
        };
    }

    // ═══════════════════════════════════════════════════════════
    // 3. 统计
    // ═══════════════════════════════════════════════════════════

    public RagStatsResponse GetStats()
    {
        int docs;
        int chunks;
        _lock.Wait();
        try
        {
            docs   = _chunks.Select(c => c.DocId).Distinct().Count();
            chunks = _chunks.Count;
        }
        finally { _lock.Release(); }

        return new RagStatsResponse { DocCount = docs, ChunkCount = chunks };
    }

    /// <summary>
    /// 返回知识库中所有文档的原文（标题 + 正文），供 fulltext 模式直接投喂 AI。
    /// </summary>
    public async Task<IReadOnlyList<(string Title, string RawText)>> GetAllDocumentsAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _documents.Values
                .Select(d => (d.Title, d.RawText))
                .ToList();
        }
        finally { _lock.Release(); }
    }

    // ═══════════════════════════════════════════════════════════
    // 辅助
    // ═══════════════════════════════════════════════════════════

    private static RagHitDto ToHit(RagChunk c, double score) => new()
    {
        ChunkId = c.ChunkId,
        DocId   = c.DocId,
        Title   = c.Title,
        Path    = c.Path,
        Content = c.Content,
        Score   = score,
    };

    private static bool TryParseJson(string text, out JsonNode? node)
    {
        node = null;
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return false;
        if (trimmed[0] != '{' && trimmed[0] != '[') return false;
        try
        {
            node = JsonNode.Parse(trimmed);
            return node is not null;
        }
        catch { return false; }
    }

    /// <summary>
    /// 把嵌套 JSON 扁平化成 (路径, 值) 序列。
    /// - 标量叶子 → "路径: 值"
    /// - 标量数组元素 → "路径[i]: 值"
    /// - 对象数组元素 → "路径[i]: {字段: 值, ...}"（整体保留，避免关联信息拆散）
    /// - 嵌套对象 → 递归到叶子
    /// </summary>
    private static IEnumerable<(string? Path, string Value)> FlattenJson(JsonNode node, string path = "")
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, child) in obj)
                {
                    if (child is null) continue;
                    var childPath = string.IsNullOrEmpty(path) ? key : $"{path}.{key}";
                    if (child is JsonValue v)
                        yield return (childPath, ExtractScalar(v));
                    else
                        foreach (var item in FlattenJson(child, childPath))
                            yield return item;
                }
                break;

            case JsonArray arr:
                // 若数组元素是对象，则每个对象整体作为一个 chunk（保留字段关联）
                var allObjects = arr.Count > 0 && arr.All(e => e is JsonObject);
                for (int i = 0; i < arr.Count; i++)
                {
                    var child = arr[i];
                    if (child is null) continue;
                    var childPath = $"{path}[{i}]";
                    if (child is JsonValue v)
                    {
                        yield return (childPath, ExtractScalar(v));
                    }
                    else if (allObjects && child is JsonObject objElem)
                    {
                        // 把对象序列化为紧凑文本，便于检索和 LLM 阅读
                        var txt = string.Join("；", objElem.Select(kv =>
                            $"{kv.Key}: {FormatNode(kv.Value)}"));
                        yield return (childPath, txt);
                    }
                    else
                    {
                        foreach (var item in FlattenJson(child, childPath))
                            yield return item;
                    }
                }
                break;
        }
    }

    private static string FormatNode(JsonNode? node) => node switch
    {
        null => "",
        JsonValue v => ExtractScalar(v),
        _ => node.ToJsonString(),
    };

    private static string ExtractScalar(JsonValue v)
    {
        // 尝试取原始字符串，失败则用 JSON 字符串（去掉外层引号）
        if (v.TryGetValue<string>(out var s)) return s;
        var json = v.ToJsonString();
        return json.Length >= 2 && json[0] == '"' && json[^1] == '"'
            ? json[1..^1]
            : json;
    }

    /// <summary>按字符数滑窗分块，保留句子边界优先。</summary>
    private static List<string> SplitText(string text, int chunkSize)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var sentences = text.Split(new[] { '。', '！', '？', '.', '!', '?', '\n' },
                                   StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var s in sentences)
        {
            if (sb.Length + s.Length > chunkSize && sb.Length > 0)
            {
                result.Add(sb.ToString().Trim());
                sb.Clear();
            }
            sb.Append(s).Append('。');
        }
        if (sb.Length > 0)
            result.Add(sb.ToString().Trim());

        return result.Count == 0 ? new List<string> { text } : result;
    }

    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            dot += a[i] * b[i];
            na  += a[i] * a[i];
            nb  += b[i] * b[i];
        }
        return na > 0 && nb > 0 ? dot / (Math.Sqrt(na) * Math.Sqrt(nb)) : 0;
    }

    // ── BM25 降级检索 ───────────────────────────────────────────
    // 当 embedding 服务不可用时使用。对中文按单字 + 英文单词分词。

    private const double Bm25K1 = 1.5;
    private const double Bm25B  = 0.75;

    private static List<(RagChunk Chunk, double Score)> Bm25Search(
        string query, IReadOnlyList<RagChunk> docs, int topK)
    {
        var qTokens = Tokenize(query);
        if (qTokens.Count == 0)
            return docs.Take(topK).Select(d => (d, 0.0)).ToList();

        var docTokens = docs.Select(d => Tokenize(d.Content)).ToList();
        var avgDl = docTokens.Count > 0
            ? docTokens.Average(t => t.Count)
            : 1;

        // 文档频率 df
        var df = new Dictionary<string, int>();
        foreach (var tokens in docTokens)
        {
            foreach (var t in tokens.Distinct())
                df[t] = df.TryGetValue(t, out var c) ? c + 1 : 1;
        }

        var N = docs.Count;
        var scores = new (RagChunk Chunk, double Score)[docs.Count];

        for (int i = 0; i < docs.Count; i++)
        {
            var tokens = docTokens[i];
            var tf = new Dictionary<string, int>();
            foreach (var t in tokens)
                tf[t] = tf.TryGetValue(t, out var c) ? c + 1 : 1;

            double score = 0;
            foreach (var qt in qTokens.Distinct())
            {
                if (!df.TryGetValue(qt, out var d) || d == 0) continue;
                tf.TryGetValue(qt, out var f);
                var idf = Math.Log(1 + (N - d + 0.5) / (d + 0.5));
                var denom = f + Bm25K1 * (1 - Bm25B + Bm25B * tokens.Count / Math.Max(avgDl, 1));
                score += idf * (f * (Bm25K1 + 1)) / denom;
            }
            scores[i] = (docs[i], score);
        }

        return scores
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();
    }

    /// <summary>中文按单字、英文/数字按连续串分词。</summary>
    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch >= 0x4E00 && ch <= 0x9FFF) // CJK 统一汉字
            {
                if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
                tokens.Add(ch.ToString());
            }
            else if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else
            {
                if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
            }
        }
        if (sb.Length > 0) tokens.Add(sb.ToString());
        return tokens;
    }

    private sealed class RagDoc
    {
        public required string DocId   { get; init; }
        public required string Title   { get; init; }
        public required string RawText { get; init; }
    }

    private sealed class RagChunk
    {
        public required string ChunkId { get; init; }
        public required string DocId   { get; init; }
        public required string Title   { get; init; }
        public string? Path            { get; init; }
        public required string Content { get; init; }
        public required float[] Vector { get; init; }
    }
}

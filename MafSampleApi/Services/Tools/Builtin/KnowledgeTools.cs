using System.ComponentModel;
using System.Text;
using MafSampleApi.Models;
using Microsoft.Extensions.AI;

namespace MafSampleApi.Services.Tools;

/// <summary>
/// 语义检索工具。演示用内存文档库，生产请替换为向量库（Qdrant / Milvus / pgvector）。
/// </summary>
public sealed class KnowledgeTools
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embedder;
    private readonly IRerankerClient _reranker;
    private readonly ILogger<KnowledgeTools> _logger;
    private readonly List<Doc> _docs = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public KnowledgeTools(
        IEmbeddingGenerator<string, Embedding<float>> embedder,
        IRerankerClient reranker,
        ILogger<KnowledgeTools> logger)
    {
        _embedder = embedder;
        _reranker = reranker;
        _logger = logger;
    }

    [Description("把一段文本加入知识库，供后续检索。")]
    public async Task<string> AddDocumentAsync(
        [Description("文档正文，不超过 4000 字。")] string content,
        [Description("文档标题（可选）。")] string? title = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "错误: content 不能为空。";
        if (content.Length > 4000)
            return "错误: content 超过 4000 字上限。";

        try
        {
            var emb = await _embedder.GenerateAsync(new[] { content });
            var vec = emb[0].Vector.ToArray();

            await _lock.WaitAsync();
            try
            {
                _docs.Add(new Doc
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = title ?? $"doc-{_docs.Count + 1}",
                    Content = content,
                    Vector = vec,
                });
            }
            finally { _lock.Release(); }

            return $"已添加，当前知识库共 {_docs.Count} 条。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddDocument 失败");
            return $"错误: {ex.Message}";
        }
    }

    [Description("根据查询语句，在知识库中检索最相关的文档片段。")]
    public async Task<string> SearchAsync(
        [Description("查询语句。")] string query,
        [Description("返回条数，默认 3，最大 10。")] int topK = 3)
    {
        if (string.IsNullOrWhiteSpace(query)) return "错误: query 不能为空。";
        topK = Math.Clamp(topK, 1, 10);

        List<Doc> snapshot;
        await _lock.WaitAsync();
        try { snapshot = _docs.ToList(); }
        finally { _lock.Release(); }

        if (snapshot.Count == 0)
            return "知识库为空，请先调用 add_document。";

        try
        {
            var qEmb = await _embedder.GenerateAsync(new[] { query });
            var qVec = qEmb[0].Vector.ToArray();

            var ranked = snapshot
                .Select(d => new { Doc = d, Score = Cosine(qVec, d.Vector) })
                .OrderByDescending(x => x.Score)
                .Take(Math.Min(topK * 3, snapshot.Count))
                .ToList();

            if (ranked.Count > 1)
            {
                try
                {
                    var docs = ranked.Select(x => x.Doc.Content).ToArray();
                    var reranked = await _reranker.RerankAsync(query, docs, topK);
                    var result = reranked.Select(r => ranked[r.Index].Doc).ToList();
                    return Format(result.Select((d, i) => (d, i, 0.0)).ToList());
                }
                catch (Exception rex)
                {
                    _logger.LogWarning(rex, "Rerank 失败，退化为纯向量检索");
                }
            }

            return Format(ranked.Take(topK)
                .Select((x, i) => (x.Doc, i, x.Score)).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search 失败");
            return $"检索失败: {ex.Message}";
        }
    }

    private static string Format(IReadOnlyList<(Doc doc, int idx, double score)> items)
    {
        if (items.Count == 0) return "无匹配结果。";

        var sb = new StringBuilder();
        for (int i = 0; i < items.Count; i++)
        {
            var (doc, _, score) = items[i];
            sb.AppendLine($"[{i + 1}] {doc.Title} (相关度 {score:F3})");
            sb.AppendLine(doc.Content.Length > 500 ? doc.Content[..500] + "…" : doc.Content);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return na > 0 && nb > 0 ? dot / (Math.Sqrt(na) * Math.Sqrt(nb)) : 0;
    }

    public IReadOnlyList<ToolDescriptor> GetTools()
    {
        var fnAdd = AIFunctionFactory.Create(
            AddDocumentAsync,
            name: "add_document",
            description: "把一段文本加入知识库，供后续检索。");

        var fnSearch = AIFunctionFactory.Create(
            SearchAsync,
            name: "search_knowledge",
            description: "根据查询语句，在知识库中检索最相关的文档片段。");

        return new[]
        {
            new ToolDescriptor
            {
                Name = "add_document", Function = fnAdd,
                Description = "往知识库添加文档",
                Tags = new[] { "knowledge", "write" },
                SafeByDefault = false,   // 写操作，默认关闭
            },
            new ToolDescriptor
            {
                Name = "search_knowledge", Function = fnSearch,
                Description = "语义检索知识库",
                Tags = new[] { "knowledge", "safe" },
            },
        };
    }

    private sealed class Doc
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required string Content { get; init; }
        public required float[] Vector { get; init; }
    }
}
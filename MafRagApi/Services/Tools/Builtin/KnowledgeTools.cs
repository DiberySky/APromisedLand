using System.ComponentModel;
using System.Text;
using APromisedLand.Api.MafRag.Dtos;
using MafRagApi.Services;
using Microsoft.Extensions.AI;

namespace MafRagApi.Services.Tools;

/// <summary>
/// 语义检索工具（Agent 工具适配层）。
/// 向量库与检索逻辑由独立的 MafVectorSearchApi 服务提供，
/// 本类通过 VectorSearchClient 调用远程服务，把结果适配为 Agent 工具的字符串契约。
/// </summary>
public sealed class KnowledgeTools
{
    private readonly VectorSearchClient _vectorSearch;
    private readonly ILogger<KnowledgeTools> _logger;

    public KnowledgeTools(
        VectorSearchClient vectorSearch,
        ILogger<KnowledgeTools> logger)
    {
        _vectorSearch = vectorSearch;
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
            var resp = await _vectorSearch.IngestAsync(new RagIngestRequest
            {
                Content = content,
                Title   = title,
            });

            var stats = await _vectorSearch.GetStatsAsync();
            return $"已添加文档（{resp.Mode} 模式，{resp.ChunkCount} 块），当前知识库共 {stats.DocCount} 条文档。";
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

        try
        {
            var resp = await _vectorSearch.RetrieveAsync(new RagRetrieveRequest
            {
                Query       = query,
                TopK        = topK,
                UseReranker = true,
            });

            if (resp.Hits.Count == 0)
                return resp.TotalChunks == 0
                    ? "知识库为空，请先调用 add_document。"
                    : "无匹配结果。";

            return Format(resp.Hits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search 失败");
            return $"检索失败: {ex.Message}";
        }
    }

    private static string Format(IReadOnlyList<RagHitDto> hits)
    {
        if (hits.Count == 0) return "无匹配结果。";

        var sb = new StringBuilder();
        for (int i = 0; i < hits.Count; i++)
        {
            var h = hits[i];
            sb.AppendLine($"[{i + 1}] {h.Title} (相关度 {h.Score:F3})");
            sb.AppendLine(h.Content.Length > 500 ? h.Content[..500] + "…" : h.Content);
            sb.AppendLine();
        }
        return sb.ToString();
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
}

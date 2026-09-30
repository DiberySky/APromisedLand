using System.Text;
using APromisedLand.Api.MafRag.Dtos;
using MafRagApi.Models;
using Microsoft.Extensions.AI;

namespace MafRagApi.Services;

/// <summary>
/// RAG 问答编排层：通过 VectorSearchClient 调用远程向量搜索服务做检索/取全文，
/// 拼装上下文后交由 IChatClient 生成答案。
/// 本类不持有向量库，仅负责"检索 → 上下文拼装 → LLM 生成"的流程编排。
/// </summary>
public sealed class RagChatOrchestrator
{
    private readonly VectorSearchClient _vectorSearch;
    private readonly IChatClient _chat;
    private readonly ILogger<RagChatOrchestrator> _logger;

    public RagChatOrchestrator(
        VectorSearchClient vectorSearch,
        IChatClient chat,
        ILogger<RagChatOrchestrator> logger)
    {
        _vectorSearch = vectorSearch;
        _chat = chat;
        _logger = logger;
    }

    public async Task<RagChatResponse> ChatAsync(RagChatRequest req, string model, CancellationToken ct = default)
    {
        string context;
        List<RagHitDto> sources;

        if (string.Equals(req.Mode, "fulltext", StringComparison.OrdinalIgnoreCase))
        {
            // ── 全文投喂模式：把所有文档原文直接交给 AI ──
            var docs = await _vectorSearch.GetAllDocumentsAsync(ct);

            if (docs.Count == 0)
            {
                return new RagChatResponse
                {
                    Query   = req.Query,
                    Answer  = "知识库为空，请先摄入文档。",
                    Sources = new(),
                    Model   = model,
                };
            }

            var sb = new StringBuilder();
            foreach (var d in docs)
                sb.AppendLine($"【文档：{d.Title}】\n{d.RawText}\n");
            context = sb.ToString();
            sources = new();
        }
        else
        {
            // ── 检索增强模式（默认）：向量检索 → 取相关片段 ──
            var retrieve = await _vectorSearch.RetrieveAsync(new RagRetrieveRequest
            {
                Query       = req.Query,
                TopK        = req.TopK,
                UseReranker = req.UseReranker,
            }, ct);
            context = BuildContext(retrieve.Hits);
            sources = retrieve.Hits;
        }

        var sysPrompt = string.Equals(req.Mode, "fulltext", StringComparison.OrdinalIgnoreCase)
            ? @"你是一个严谨的问答助手。请仔细阅读下面的【文档全文】，从中找出用户问题的答案。
- 文档中没有的信息，直接回答「根据现有资料无法回答」，不要编造。
- 回答要简洁准确，可引用文档中的内容。"
            : @"你是一个严谨的问答助手。请严格依据下面的【参考资料】回答用户问题。
- 资料中没有的信息，直接回答「根据现有资料无法回答」，不要编造。
- 回答要简洁准确，可引用资料中的路径。";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, sysPrompt),
            new(ChatRole.User, $"【{(sources.Count == 0 ? "文档全文" : "参考资料")}】\n{context}\n\n【用户问题】\n{req.Query}"),
        };

        var options = new ChatOptions { ModelId = model };

        // 可选结构化输出
        if (req.UseStructuredOutput)
        {
            var rf = StructuredOutput.BuildResponseFormat(req.ResponseSchema, req.OutputFormat);
            if (rf is not null)
            {
                if (options.AdditionalProperties is null)
                    options.AdditionalProperties = new();
                options.AdditionalProperties[StructuredOutput.AdditionalPropertiesKey] = rf;
            }
        }

        var resp = await _chat.GetResponseAsync(messages, options, ct);
        var answer = resp.Messages.FirstOrDefault()?.Text ?? "";

        return new RagChatResponse
        {
            Query   = req.Query,
            Answer  = answer,
            Sources = sources,
            Model   = model,
        };
    }

    private static string BuildContext(IReadOnlyList<RagHitDto> hits)
    {
        if (hits.Count == 0) return "（无相关资料）";

        var sb = new StringBuilder();
        for (int i = 0; i < hits.Count; i++)
        {
            sb.AppendLine($"[{i + 1}] {hits[i].Content}");
        }
        return sb.ToString();
    }
}

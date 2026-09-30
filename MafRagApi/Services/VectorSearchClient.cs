using System.Net.Http.Json;
using APromisedLand.Api.MafRag.Dtos;

namespace MafRagApi.Services;

/// <summary>
/// 对 MafVectorSearchApi 的 HTTP 客户端封装。
/// 负责调用远程向量搜索服务的 ingest / retrieve / stats / fulltext 接口。
/// </summary>
public sealed class VectorSearchClient(HttpClient http)
{
    /// <summary>摄入文档（JSON 自动扁平化分块；普通文本按字符数分块）。</summary>
    public async Task<RagIngestResponse> IngestAsync(RagIngestRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("api/rag/ingest", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<RagIngestResponse>(ct))!;
    }

    /// <summary>检索与 query 最相关的知识片段。</summary>
    public async Task<RagRetrieveResponse> RetrieveAsync(RagRetrieveRequest request, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsync("api/rag/retrieve", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<RagRetrieveResponse>(ct))!;
    }

    /// <summary>查看知识库统计（文档数、分块数）。</summary>
    public async Task<RagStatsResponse> GetStatsAsync(CancellationToken ct = default)
    {
        return (await http.GetFromJsonAsync<RagStatsResponse>("api/rag/stats", ct))!;
    }

    /// <summary>获取知识库所有文档原文（标题 + 正文），供 fulltext 模式投喂 AI。</summary>
    public async Task<IReadOnlyList<(string Title, string RawText)>> GetAllDocumentsAsync(CancellationToken ct = default)
    {
        var docs = await http.GetFromJsonAsync<List<FulltextDoc>>("api/rag/fulltext", ct)
                   ?? new List<FulltextDoc>();
        return docs.Select(d => (d.Title, d.RawText)).ToList();
    }

    private sealed class FulltextDoc
    {
        public string Title   { get; set; } = "";
        public string RawText { get; set; } = "";
    }
}

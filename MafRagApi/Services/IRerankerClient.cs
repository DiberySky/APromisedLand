using APromisedLand.Api.MafRag.Dtos;

namespace MafRagApi.Services;

/// <summary>Reranker 客户端抽象。</summary>
public interface IRerankerClient
{
    /// <summary>对 documents 按 query 相关性重排，返回 topK 条降序结果。</summary>
    Task<IReadOnlyList<RerankResultDto>> RerankAsync(
        string query,
        IReadOnlyList<string> documents,
        int? topK = null,
        CancellationToken ct = default);
}
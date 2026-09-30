using APromisedLand.Api.MafRag.Dtos;
using MafVectorSearchApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MafVectorSearchApi.Controllers;

/// <summary>
/// 文档摄入与语义检索入口。
/// 路由前缀：/api/rag
/// 说明：chat（问答编排）由 MafRagApi 负责，本服务仅提供 ingest/retrieve/stats。
/// </summary>
[ApiController]
[Route("api/rag")]
[Produces("application/json")]
public sealed class RagController(
    RagService rag,
    ILogger<RagController> logger) : ControllerBase
{
    /// <summary>摄入文档（JSON 自动扁平化分块；普通文本按字符数分块）。</summary>
    [HttpPost("ingest")]
    public async Task<ActionResult<RagIngestResponse>> Ingest(
        [FromBody] RagIngestRequest request,
        CancellationToken ct)
    {
        try
        {
            var resp = await rag.IngestAsync(request, ct);
            return Ok(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RAG ingest 失败");
            return StatusCode(500, new ProblemDetails
            {
                Title  = "摄入失败",
                Detail = ex.Message,
                Status = 500,
            });
        }
    }

    /// <summary>检索与 query 最相关的知识片段（不生成回答）。</summary>
    [HttpPost("retrieve")]
    public async Task<ActionResult<RagRetrieveResponse>> Retrieve(
        [FromBody] RagRetrieveRequest request,
        CancellationToken ct)
    {
        try
        {
            var resp = await rag.RetrieveAsync(request, ct);
            return Ok(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RAG retrieve 失败");
            return StatusCode(500, new ProblemDetails
            {
                Title  = "检索失败",
                Detail = ex.Message,
                Status = 500,
            });
        }
    }

    /// <summary>查看知识库统计（文档数、分块数）。</summary>
    [HttpGet("stats")]
    public ActionResult<RagStatsResponse> Stats() => Ok(rag.GetStats());

    /// <summary>返回知识库所有文档原文（标题 + 正文），供 fulltext 模式投喂 AI。</summary>
    [HttpGet("fulltext")]
    public async Task<ActionResult<IReadOnlyList<object>>> Fulltext(CancellationToken ct)
    {
        var docs = await rag.GetAllDocumentsAsync(ct);
        return Ok(docs.Select(d => new { d.Title, d.RawText }).ToList());
    }
}

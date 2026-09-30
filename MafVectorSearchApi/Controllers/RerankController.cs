using APromisedLand.Api.MafRag.Dtos;
using MafVectorSearchApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MafVectorSearchApi.Controllers;

/// <summary>
/// 文档重排序入口（调用外部 reranker 服务）。
/// 路由前缀：/api/rerank
/// </summary>
[ApiController]
[Route("api/rerank")]
[Produces("application/json")]
public sealed class RerankController(
    IRerankerClient reranker,
    ILogger<RerankController> logger) : ControllerBase
{
    /// <summary>对一组文档按 query 相关性重排序。</summary>
    [HttpPost]
    public async Task<ActionResult<RerankResponseDto>> Rerank(
        [FromBody] RerankRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new ProblemDetails { Title = "query 不能为空。", Status = 400 });

        if (request.Documents is null || request.Documents.Count == 0)
            return BadRequest(new ProblemDetails { Title = "documents 不能为空。", Status = 400 });

        try
        {
            var results = await reranker.RerankAsync(
                request.Query, request.Documents, request.TopK, ct);

            return Ok(new RerankResponseDto
            {
                Query          = request.Query,
                TotalDocuments = request.Documents.Count,
                Results        = results,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rerank 失败");
            return StatusCode(500, new ProblemDetails
            {
                Title  = "重排序失败",
                Detail = ex.Message,
                Status = 500,
            });
        }
    }
}

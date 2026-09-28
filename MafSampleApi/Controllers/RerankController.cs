using MafSampleApi.Models;
using MafSampleApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MafSampleApi.Controllers;

/// <summary>语义重排端点：POST /api/rerank。</summary>
[ApiController]
[Route("api/rerank")]
[Produces("application/json")]
public sealed class RerankController(
    IRerankerClient reranker,
    IOptions<RerankerOptions> options,
    ILogger<RerankController> logger) : ControllerBase
{
    private readonly RerankerOptions _options = options.Value;

    [HttpPost]
    public async Task<ActionResult<RerankResponseDto>> RerankAsync(
        [FromBody] RerankRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "query is required." });

        if (request.Documents is null || request.Documents.Count == 0)
            return BadRequest(new { error = "documents must not be empty." });

        if (request.Documents.Count > _options.MaxDocuments)
            return BadRequest(new
            {
                error = $"documents.Count ({request.Documents.Count}) exceeds MaxDocuments ({_options.MaxDocuments})."
            });

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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Rerank failed");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }
}
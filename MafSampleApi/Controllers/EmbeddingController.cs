using MafSampleApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MafSampleApi.Controllers;

/// <summary>
/// 文本向量化入口（vLLM bge-m3）。
/// 路由前缀：/api/embedding
/// </summary>
[ApiController]
[Route("api/embedding")]
[Produces("application/json")]
public sealed class EmbeddingController(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    IOptions<AgentOptions> agentOptions,
    ILogger<EmbeddingController> logger) : ControllerBase
{
    private readonly AgentOptions _options = agentOptions.Value;

    /// <summary>暴露当前使用的模型名与维度。</summary>
    [HttpGet("model")]
    public ActionResult<object> GetModel() => Ok(new
    {
        Model     = _options.EmbeddingModel,
        Dimension = _options.EmbeddingDimension
    });

    /// <summary>将文本转为向量。</summary>
    [HttpPost("embed")]
    public async Task<ActionResult<EmbedResponse>> Embed(
        [FromBody] EmbedRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new ProblemDetails
            {
                Title  = "Text 不能为空。",
                Status = StatusCodes.Status400BadRequest
            });

        try
        {
            var result = await generator.GenerateAsync(
                new[] { request.Text }, cancellationToken: ct);

            if (result.Count == 0)
            {
                logger.LogWarning("Embedding 返回空结果");
                return StatusCode(502, new ProblemDetails
                {
                    Title  = "Embedding 服务返回空结果。",
                    Status = StatusCodes.Status502BadGateway
                });
            }

            var vector = result[0].Vector.ToArray().ToList();

            logger.LogDebug(
                "Embedding({Model}) 返回向量: {Dimension}",
                _options.EmbeddingModel, vector.Count);

            return Ok(new EmbedResponse(
                Vector:    vector,
                Dimension: vector.Count,
                Model:     _options.EmbeddingModel));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "生成 embedding 失败");
            return StatusCode(502, new ProblemDetails
            {
                Title  = "生成 embedding 失败。",
                Detail = ex.Message,
                Status = StatusCodes.Status502BadGateway
            });
        }
    }
}
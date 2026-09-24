using MAFWorkFlowApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace MAFWorkFlowApi.Controllers;

/// <summary>
/// 文本向量化入口。转发到 Ollama（通过 Microsoft.Extensions.AI 抽象）。
/// 路由前缀：/api/embedding
/// </summary>
[ApiController]
[Route("api/embedding")]
[Produces("application/json")]
public sealed class EmbeddingController : ControllerBase
{
    private const string EmbeddingServiceKey = "embedding";

    private readonly IServiceProvider _sp;
    private readonly ILogger<EmbeddingController> _logger;
    private readonly string _modelName;   // ★ 从配置注入

    public EmbeddingController(
        IServiceProvider sp,
        ILogger<EmbeddingController> logger,
        IConfiguration configuration)
    {
        _sp = sp;
        _logger = logger;
        _modelName = configuration["Embedding:Model"] ?? "bge-m3";
        // ★ 从 Info 降为 Debug —— 避免每个请求打日志
        _logger.LogDebug("EmbeddingController 使用模型: {Model}", _modelName);
    }

    /// <summary>★ 新增：暴露当前模型信息，供前端/调试使用。</summary>
    [HttpGet("model")]
    public ActionResult<object> GetModel() => Ok(new
    {
        Model = _modelName,
        ServiceKey = EmbeddingServiceKey
    });

    /// <summary>将文本转为向量。前端不再直连 Ollama。</summary>
    [HttpPost("embed")]
    public async Task<ActionResult<EmbedResponse>> Embed(
        [FromBody] EmbedRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new ProblemDetails
            {
                Title = "Text 不能为空。",
                Status = StatusCodes.Status400BadRequest
            });

        try
        {
            var generator = _sp.GetRequiredKeyedService<
                IEmbeddingGenerator<string, Embedding<float>>>(EmbeddingServiceKey);

            var result = await generator.GenerateAsync(
                new[] { request.Text },
                cancellationToken: ct);

            if (result.Count == 0)
            {
                _logger.LogWarning("Embedding 返回空结果");
                return StatusCode(502, new ProblemDetails
                {
                    Title = "Embedding 服务返回空结果。",
                    Status = StatusCodes.Status502BadGateway
                });
            }

            var embedding = result[0];
            var vector = embedding.Vector.ToArray().ToList();

            _logger.LogDebug(
                "Embedding({Model}) 返回向量: {Dimension}",
                _modelName, vector.Count);

            return Ok(new EmbedResponse(
                Vector: vector,
                Dimension: vector.Count,
                Model: _modelName));     // ★ 用注入的模型名
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成 embedding 失败");
            return StatusCode(502, new ProblemDetails
            {
                Title = "生成 embedding 失败。",
                Detail = ex.Message,
                Status = StatusCodes.Status502BadGateway
            });
        }
    }
}
using MafSampleApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace MafSampleApi.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(
    OllamaApiClient ollama,
    IOptions<AgentOptions> agentOptions) : ControllerBase
{
    private readonly AgentOptions _agentOptions = agentOptions.Value;

    /// <summary>轻量健康检查（无需连通 Ollama）。</summary>
    [HttpGet]
    public ActionResult<HealthResponseDto> Get()
        => Ok(new HealthResponseDto
        {
            ChatModel = _agentOptions.ChatModel,
            EmbeddingModel = _agentOptions.EmbeddingModel
        });

    /// <summary>深度健康检查：尝试列出 Ollama 模型列表。</summary>
    [HttpGet("deep")]
    public async Task<ActionResult<HealthResponseDto>> DeepAsync(CancellationToken ct)
    {
        try
        {
            // ListLocalModelsAsync 返回 Task<IEnumerable<Model>>
            var models = await ollama.ListLocalModelsAsync(ct);
            var count = models.Count();

            return Ok(new HealthResponseDto
            {
                Status = $"ok ({count} models)",
                ChatModel = _agentOptions.ChatModel,
                EmbeddingModel = _agentOptions.EmbeddingModel
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new HealthResponseDto
            {
                Status = $"unreachable: {ex.Message}",
                ChatModel = _agentOptions.ChatModel,
                EmbeddingModel = _agentOptions.EmbeddingModel
            });
        }
    }
}
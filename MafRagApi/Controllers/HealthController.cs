using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MafRagApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MafRagApi.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> agentOptions,
    ILogger<HealthController> logger) : ControllerBase
{
    private readonly AgentOptions _options = agentOptions.Value;

    /// <summary>轻量：仅回显配置，永远 200（liveness）。</summary>
    [HttpGet]
    public ActionResult<HealthResponseDto> Get()
        => Ok(new HealthResponseDto
        {
            ChatModel      = _options.ChatModel,
            EmbeddingModel = _options.EmbeddingModel,
        });

    /// <summary>深度：探活 vLLM /v1/models（readiness）。</summary>
    [HttpGet("deep")]
    public async Task<ActionResult<HealthResponseDto>> DeepAsync(CancellationToken ct)
    {
        try
        {
            var http = httpClientFactory.CreateClient("vllm");
            var payload = await http.GetFromJsonAsync<VllmModelsResponse>("v1/models", ct);

            var ids = payload?.Data?
                .Select(m => m.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToArray() ?? Array.Empty<string>();

            if (ids.Length == 0)
            {
                logger.LogWarning("vLLM reachable but exposed no models");
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new HealthResponseDto
                    {
                        Status = "vllm reachable but no models loaded",
                        ChatModel = _options.ChatModel,
                        EmbeddingModel = _options.EmbeddingModel,
                    });
            }

            var expected = _options.ChatModel;
            var found = ids.Contains(expected, StringComparer.OrdinalIgnoreCase);

            return Ok(new HealthResponseDto
            {
                Status = found
                    ? $"ok ({ids.Length} models, serving '{expected}')"
                    : $"degraded (expected '{expected}' not found; have: {string.Join(", ", ids)})",
                ChatModel = _options.ChatModel,
                EmbeddingModel = _options.EmbeddingModel,
            });
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "vLLM deep health check timed out");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new HealthResponseDto
                {
                    Status = "vllm timeout (5s)",
                    ChatModel = _options.ChatModel,
                    EmbeddingModel = _options.EmbeddingModel,
                });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "vLLM deep health check failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new HealthResponseDto
                {
                    Status = $"vllm unreachable: {ex.Message}",
                    ChatModel = _options.ChatModel,
                    EmbeddingModel = _options.EmbeddingModel,
                });
        }
    }

    private sealed class VllmModelsResponse
    {
        [JsonPropertyName("data")]
        public List<VllmModel>? Data { get; set; }
    }

    private sealed class VllmModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }
}
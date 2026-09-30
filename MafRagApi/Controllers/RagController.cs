using MafRagApi.Models;
using MafRagApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MafRagApi.Controllers;

/// <summary>
/// RAG 问答入口（检索编排 + LLM 生成）。
/// 路由前缀：/api/rag
/// 说明：ingest/retrieve/stats 已拆至 MafVectorSearchApi，本控制器仅保留 chat。
/// </summary>
[ApiController]
[Route("api/rag")]
[Produces("application/json")]
public sealed class RagController(
    RagChatOrchestrator chatOrchestrator,
    IOptions<AgentOptions> agentOptions,
    ILogger<RagController> logger) : ControllerBase
{
    private readonly AgentOptions _opts = agentOptions.Value;

    /// <summary>RAG 问答：检索 → 拼装上下文 → 模型生成回答。</summary>
    [HttpPost("chat")]
    public async Task<ActionResult<RagChatResponse>> Chat(
        [FromBody] RagChatRequest request,
        CancellationToken ct)
    {
        try
        {
            var resp = await chatOrchestrator.ChatAsync(request, _opts.ChatModel, ct);
            return Ok(resp);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RAG chat 失败");
            return StatusCode(500, new ProblemDetails
            {
                Title  = "RAG 问答失败",
                Detail = ex.Message,
                Status = 500,
            });
        }
    }
}

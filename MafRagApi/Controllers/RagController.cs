using MafRagApi.Models;
using MafRagApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MafRagApi.Controllers;

/// <summary>
/// RAG（检索增强生成）入口。
/// 路由前缀：/api/rag
/// </summary>
[ApiController]
[Route("api/rag")]
[Produces("application/json")]
public sealed class RagController(
    RagService rag,
    IOptions<AgentOptions> agentOptions,
    ILogger<RagController> logger) : ControllerBase
{
    private readonly AgentOptions _opts = agentOptions.Value;

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

    /// <summary>RAG 问答：检索 → 拼装上下文 → 模型生成回答。</summary>
    [HttpPost("chat")]
    public async Task<ActionResult<RagChatResponse>> Chat(
        [FromBody] RagChatRequest request,
        CancellationToken ct)
    {
        try
        {
            var resp = await rag.ChatAsync(request, _opts.ChatModel, ct);
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

    /// <summary>查看知识库统计（文档数、分块数）。</summary>
    [HttpGet("stats")]
    public ActionResult<RagStatsResponse> Stats() => Ok(rag.GetStats());
}

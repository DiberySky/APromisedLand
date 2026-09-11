using MAFRagService.Agents;
using MAFRagService.Models;
using Microsoft.AspNetCore.Mvc;

namespace MAFRagService.Controllers;

[ApiController]
[Route("rag/ask")]
public class AskController : BaseApiController
{
    private readonly OrchestratorAgent? _orchestrator;

    public AskController(IServiceProvider sp, IConfiguration config) : base(config)
    {
        _orchestrator = sp.GetService<OrchestratorAgent>();
    }

    // ============================================================
    // POST /rag/ask
    //
    // 请求体：
    //   {
    //     "question": "阿里巴巴的创始人是谁？",
    //     "tenant":   "tenant1",     // 可选，被 X-Tenant-Id 或 JWT claim 覆盖
    //     "version":  null,          // 可选
    //     "useGraph": true,          // 可选
    //     "topK":     5              // 可选
    //   }
    //
    // 响应体：
    //   {
    //     "answer":  "...",
    //     "sources": [ { "docId": "...", "content": "...", "score": 0.87 } ],
    //     "tenant":  "tenant1"
    //   }
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] AskRequest req)
    {
        if (!Features.Rag)         return ModuleDisabled("Rag");
        if (_orchestrator is null) return ModuleDisabled("Rag.Orchestrator");

        if (string.IsNullOrWhiteSpace(req.Question))
            return BadRequest("Question is required.");

        var tenant = ResolveTenant();

        var result = await _orchestrator.AskQuestion(
            req.Question,
            tenant,
            req.Version,
            req.UseGraph ?? Features.Graph,
            req.TopK ?? 5);

        // ★ 返回完整结构：answer + sources + tenant
        return Ok(new
        {
            Answer  = result.Answer,
            Sources = result.Sources,
            Tenant  = result.Tenant
        });
    }
}
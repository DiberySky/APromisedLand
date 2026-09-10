using MAFRagService.Agents;
using MAFRagService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAFRagService.Controllers;

[ApiController]
[Route("rag/ask")]
[Authorize]
public class AskController : BaseApiController
{
    private readonly OrchestratorAgent _orchestrator;

    public AskController(
        OrchestratorAgent orchestrator,
        IConfiguration config)
        : base(config)
    {
        _orchestrator = orchestrator;
    }

    // ============================================================
    // POST /rag/ask
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] AskRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Question))
            return BadRequest("Question is required.");

        var tenant = ResolveTenant();

        var answer = await _orchestrator.AskQuestion(
            req.Question,
            tenant,
            req.Version,
            req.UseGraph ?? true,
            req.TopK ?? 5);

        return Ok(new { Answer = answer, Tenant = tenant });
    }
}
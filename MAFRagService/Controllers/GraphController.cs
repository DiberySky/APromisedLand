using MAFRagService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MAFRagService.Controllers;

[ApiController]
[Route("rag/graph")]
[Authorize]
public class GraphController : BaseApiController
{
    private readonly KnowledgeGraphService _graph;

    public GraphController(
        KnowledgeGraphService graph,
        IConfiguration config)
        : base(config)
    {
        _graph = graph;
    }

    // ============================================================
    // GET /rag/graph/communities?tenant=xxx
    // ============================================================
    [HttpGet("communities")]
    public IActionResult Communities([FromQuery] string tenant)
    {
        // 保留原占位实现
        return Ok(new
        {
            Message = "Community detection not implemented in this version"
        });
    }
}
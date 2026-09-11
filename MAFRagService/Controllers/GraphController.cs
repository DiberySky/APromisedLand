using MAFRagService.Services;
using Microsoft.AspNetCore.Mvc;

namespace MAFRagService.Controllers;

[ApiController]
[Route("rag/graph")]
public class GraphController : BaseApiController
{
    private readonly KnowledgeGraphService? _graph;

    public GraphController(IServiceProvider sp, IConfiguration config) : base(config)
    {
        _graph = sp.GetService<KnowledgeGraphService>();
    }

    [HttpGet("communities")]
    public IActionResult Communities([FromQuery] string tenant)
    {
        if (!Features.Graph || _graph is null) return ModuleDisabled("Graph");

        return Ok(new
        {
            Message = "Community detection not implemented in this version"
        });
    }
}
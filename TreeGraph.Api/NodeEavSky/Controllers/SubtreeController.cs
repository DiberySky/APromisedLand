using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.NodeEavSky.Services;

namespace TreeGraph.Api.NodeEavSky.Controllers;

[ApiController]
[Route("api/inode/{hostType}/{inodeId}/subtree")]
public sealed class SubtreeController : ControllerBase
{
    private readonly SubtreeService _subtree;

    public SubtreeController(SubtreeService subtree) => _subtree = subtree;

    [HttpGet]
    public async Task<IActionResult> Get(
        string hostType, string inodeId,
        [FromQuery] int maxDepth = 3,
        [FromQuery] int maxNodes = 50,
        CancellationToken ct = default)
    {
        maxDepth = Math.Clamp(maxDepth, 1, 5);
        maxNodes = Math.Clamp(maxNodes, 1, 200);

        var result = await _subtree.LoadAsync(
            hostType, inodeId, maxDepth, maxNodes, ct);

        if (result.Root is null) return NotFound();
        return Ok(result);
    }
}

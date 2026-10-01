using APromisedLand.Shared.TreeGraph.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraphApi.Services;

namespace TreeGraphApi.Controllers;

[ApiController]
[Route("api/tree")]
public class TreeController : ControllerBase
{
    private readonly ITreeService _service;

    public TreeController(ITreeService service) => _service = service;

    [HttpGet("roots")]
    public async Task<IActionResult> GetRoots([FromQuery] TreeQueryOptions opts, CancellationToken ct)
        => Ok(await _service.GetRootsAsync(opts, ct));

    [HttpGet("{parentId}/children")]
    public async Task<IActionResult> GetChildren(string parentId, CancellationToken ct)
        => Ok(await _service.GetChildrenAsync(parentId, ct));

    [HttpGet("{rootId}/subtree")]
    public async Task<IActionResult> GetSubtree(string rootId, CancellationToken ct)
        => Ok(await _service.GetSubtreeAsync(rootId, ct));

    /// <summary>
    /// 优化 #5:接收 TreeNodeDto 而非 TreeNodeEntity,避免暴露数据库结构。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TreeNodeDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetChildren),
            new { parentId = created.Id }, created);
    }

    /// <summary>
    /// 优化 #7:UpdateAsync 失败时返回 409 Conflict,提示前端并发冲突。
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] TreeNodeDto dto, CancellationToken ct)
    {
        try
        {
            dto.Id = id;
            var updated = await _service.UpdateAsync(id, dto, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "The node was modified by another client. Please refresh and retry." });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{nodeId}/move/{newParentId}")]
    public async Task<IActionResult> Move(string nodeId, string newParentId, CancellationToken ct)
    {
        try
        {
            await _service.MoveAsync(nodeId, newParentId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

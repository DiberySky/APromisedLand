using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Controllers;

/// <summary>
/// Edge Instance CRUD + 图查询。
///
/// 路由：
///   CRUD：  POST/GET/PUT/DELETE /api/eav/edges/{edgeTypeId}[/edgeId]
///   图查询：GET /api/eav/edges/{edgeTypeId}/from|to/{nodeId}
///           POST /api/eav/edges/{edgeTypeId}/traverse
/// </summary>
[ApiController]
[Route("api/eav/edges/{edgeTypeId}")]
public class EdgeController : ControllerBase
{
    private readonly EdgeInstanceService _service;

    public EdgeController(EdgeInstanceService service) => _service = service;

    /// <summary>列出某类型的所有边。</summary>
    [HttpGet]
    public async Task<IActionResult> List(string edgeTypeId, CancellationToken ct)
    {
        var list = await _service.ListByTypeAsync(edgeTypeId, ct);
        return Ok(list);
    }

    /// <summary>创建边。</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        string edgeTypeId, [FromBody] CreateEdgeRequest req, CancellationToken ct)
    {
        try
        {
            var d = await _service.CreateAsync(edgeTypeId, req, ct);
            return d is null
                ? NotFound(new { error = $"EdgeType 不存在: {edgeTypeId}" })
                : CreatedAtAction(nameof(Get),
                    new { edgeTypeId, edgeId = d.EdgeId }, d);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    /// <summary>按 ID 获取边。</summary>
    [HttpGet("{edgeId}")]
    public async Task<IActionResult> Get(
        string edgeTypeId, string edgeId, CancellationToken ct)
    {
        var d = await _service.GetByIdAsync(edgeId, ct);
        if (d is null) return NotFound();
        // 类型不匹配时也返回 NotFound
        return d.EdgeTypeId == edgeTypeId ? Ok(d) : NotFound();
    }

    /// <summary>更新边（目前仅支持 Weight）。</summary>
    [HttpPut("{edgeId}")]
    public async Task<IActionResult> Update(
        string edgeTypeId, string edgeId,
        [FromBody] UpdateEdgeRequest req, CancellationToken ct)
    {
        var d = await _service.UpdateAsync(edgeId, req, ct);
        if (d is null) return NotFound();
        return d.EdgeTypeId == edgeTypeId ? Ok(d) : NotFound();
    }

    /// <summary>删除边。</summary>
    [HttpDelete("{edgeId}")]
    public async Task<IActionResult> Delete(
        string edgeTypeId, string edgeId, CancellationToken ct)
    {
        // 校验路径中的 edgeTypeId 与实际所属类型一致（与 Get/Update 行为对齐）
        var d = await _service.GetByIdAsync(edgeId, ct);
        if (d is null || d.EdgeTypeId != edgeTypeId) return NotFound();

        var ok = await _service.DeleteAsync(edgeId, ct);
        return ok ? NoContent() : NotFound();
    }

    // ============================================================
    // 图查询
    // ============================================================

    /// <summary>查某节点的出边（from → *）。</summary>
    [HttpGet("from/{nodeId}")]
    public async Task<IActionResult> Outgoing(
        string edgeTypeId, string nodeId, CancellationToken ct)
        => Ok(await _service.FindOutgoingAsync(edgeTypeId, nodeId, ct));

    /// <summary>查某节点的入边（* → to）。</summary>
    [HttpGet("to/{nodeId}")]
    public async Task<IActionResult> Incoming(
        string edgeTypeId, string nodeId, CancellationToken ct)
        => Ok(await _service.FindIncomingAsync(edgeTypeId, nodeId, ct));

    /// <summary>边遍历（一跳，Direction = out/in/both）。</summary>
    [HttpPost("traverse")]
    public async Task<IActionResult> Traverse(
        string edgeTypeId, [FromBody] EdgeTraverseRequest req, CancellationToken ct)
        => Ok(await _service.TraverseAsync(edgeTypeId, req, ct));
}

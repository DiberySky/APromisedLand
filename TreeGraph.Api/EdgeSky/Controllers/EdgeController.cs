using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.EdgeSky.Services;
using TreeGraph.Shared.EdgeSky.Dtos;

namespace TreeGraph.Api.EdgeSky.Controllers;

/// <summary>
/// 边实例 CRUD + 图查询（R4 全局路由）。
///
/// 路由：
///   CRUD：  GET/POST /api/eav/edges、GET/PUT/DELETE /api/eav/edges/{edgeId}
///   图查询：GET /api/eav/edges/from|to/{nodeId}?typeName=、POST /api/eav/edges/traverse
///
/// 异常统一交由 ApiEnvelopeFilter 映射：
///   EavValidationException / ArgumentException → 400；InvalidOperationException → 409。
/// </summary>
[ApiController]
[Route("api/eav/edges")]
public class EdgeController : ControllerBase
{
    private readonly EdgeInstanceService _service;

    public EdgeController(EdgeInstanceService service) => _service = service;

    /// <summary>全局边列表；nodeIds 为逗号分隔，命中 From 或 To 即返回。</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? typeName = null,
        [FromQuery] string? nodeIds = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        string[]? ids = null;
        if (!string.IsNullOrWhiteSpace(nodeIds))
        {
            ids = nodeIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        var list = await _service.ListAsync(
            string.IsNullOrWhiteSpace(typeName) ? null : typeName,
            ids,
            string.IsNullOrWhiteSpace(search) ? null : search,
            ct);

        return Ok(list);
    }

    /// <summary>创建边（类型名在 body 中）。</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateEdgeRequest req, CancellationToken ct)
    {
        var d = await _service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { edgeId = d.EdgeId }, d);
    }

    /// <summary>按 ID 获取边。</summary>
    [HttpGet("{edgeId}")]
    public async Task<IActionResult> Get(string edgeId, CancellationToken ct)
    {
        var d = await _service.GetByIdAsync(edgeId, ct);
        return d is null ? NotFound() : Ok(d);
    }

    /// <summary>更新边（仅支持 IsDirected；属性走 EAV 端点）。</summary>
    [HttpPut("{edgeId}")]
    public async Task<IActionResult> Update(
        string edgeId, [FromBody] UpdateEdgeRequest req, CancellationToken ct)
    {
        var d = await _service.UpdateAsync(edgeId, req, ct);
        return d is null ? NotFound() : Ok(d);
    }

    /// <summary>删除边（不级联：仍有属性值/实体数据/被引用时服务抛 InvalidOperationException → 409）。</summary>
    [HttpDelete("{edgeId}")]
    public async Task<IActionResult> Delete(string edgeId, CancellationToken ct)
        => await _service.DeleteAsync(edgeId, ct) ? NoContent() : NotFound();

    // ============================================================
    // 图查询
    // ============================================================

    /// <summary>查某节点的出边（有向 from；无向双向）。</summary>
    [HttpGet("from/{nodeId}")]
    public async Task<IActionResult> Outgoing(
        string nodeId, [FromQuery] string? typeName, CancellationToken ct)
        => Ok(await _service.FindOutgoingAsync(typeName, nodeId, ct));

    /// <summary>查某节点的入边（有向 to；无向双向）。</summary>
    [HttpGet("to/{nodeId}")]
    public async Task<IActionResult> Incoming(
        string nodeId, [FromQuery] string? typeName, CancellationToken ct)
        => Ok(await _service.FindIncomingAsync(typeName, nodeId, ct));

    /// <summary>边遍历（Direction = out/in/both）。</summary>
    [HttpPost("traverse")]
    public async Task<IActionResult> Traverse(
        [FromBody] EdgeTraverseRequest req, CancellationToken ct)
        => Ok(await _service.TraverseAsync(req.TypeName, req, ct));
}

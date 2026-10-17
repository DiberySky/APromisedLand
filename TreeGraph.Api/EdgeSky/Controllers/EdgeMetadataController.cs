using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.EdgeSky.Services;
using TreeGraph.Shared.EdgeSky.Dtos;

namespace TreeGraph.Api.EdgeSky.Controllers;

/// <summary>
/// Edge Definition CRUD（关系类型元数据）。
/// 路由：/api/eav/metadata/edge-types
/// </summary>
[ApiController]
[Route("api/eav/metadata/edge-types")]
public class EdgeMetadataController : ControllerBase
{
    private readonly EdgeMetadataService _service;

    public EdgeMetadataController(EdgeMetadataService service) => _service = service;

    /// <summary>列出所有边类型。</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));

    /// <summary>按 ID 获取边类型。</summary>
    [HttpGet("{edgeTypeId}")]
    public async Task<IActionResult> Get(string edgeTypeId, CancellationToken ct)
    {
        var d = await _service.GetByIdAsync(edgeTypeId, ct);
        return d is null ? NotFound() : Ok(d);
    }

    /// <summary>创建边类型。</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateEdgeTypeRequest req, CancellationToken ct)
    {
        try
        {
            var d = await _service.CreateAsync(req, ct);
            return CreatedAtAction(nameof(Get), new { edgeTypeId = d.EdgeTypeId }, d);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    /// <summary>更新边类型。</summary>
    [HttpPut("{edgeTypeId}")]
    public async Task<IActionResult> Update(
        string edgeTypeId, [FromBody] UpdateEdgeTypeRequest req, CancellationToken ct)
    {
        try
        {
            var d = await _service.UpdateAsync(edgeTypeId, req, ct);
            return d is null ? NotFound() : Ok(d);
        }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// 删除边类型（硬删）。
    /// 不采用级联删除：边实例 / 活动属性非空时返回 409，需先清空子项与引用。
    /// </summary>
    [HttpDelete("{edgeTypeId}")]
    public async Task<IActionResult> Delete(string edgeTypeId, CancellationToken ct)
    {
        try
        {
            var r = await _service.DeleteAsync(edgeTypeId, ct);
            return r.Deleted ? Ok(r) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}

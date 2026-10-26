using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.EdgeSky.Services;
using TreeGraph.Shared.EdgeSky.Dtos;

namespace TreeGraph.Api.EdgeSky.Controllers;

/// <summary>
/// EdgeType 管理 API（/api/edge/types）：镜像 NebulaGraph 边类型语义。
/// 名称唯一不可改名；TTL/注释仅创建时可设；变更仅限 ALTER ADD/DROP/CHANGE 属性。
/// 异常出口统一走 ApiEnvelopeFilter：400=参数/校验，404=不存在，409=业务规则冲突。
/// </summary>
[ApiController]
[Route("api/edge/types")]
public class EdgeTypesController(EdgeTypeService svc) : ControllerBase
{
    // GET /api/edge/types
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await svc.ListAsync(ct));

    // GET /api/edge/types/{name}
    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name, CancellationToken ct)
        => Ok(await svc.GetAsync(name, ct));

    // POST /api/edge/types
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEdgeTypeRequest req, CancellationToken ct)
    {
        var dto = await svc.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { name = dto.Name }, dto);
    }

    // PUT /api/edge/types/{name}（ALTER：仅属性集增/删/改类型）
    [HttpPut("{name}")]
    public async Task<IActionResult> Alter(string name, [FromBody] AlterEdgeTypeRequest req, CancellationToken ct)
        => Ok(await svc.AlterAsync(name, req, ct));

    // PATCH /api/edge/types/{name}/display-name（非 Nebula 概念，仅更新中文展示名）
    [HttpPatch("{name}/display-name")]
    public async Task<IActionResult> UpdateDisplayName(
        string name, [FromBody] UpdateEdgeTypeDisplayNameRequest req, CancellationToken ct)
        => Ok(await svc.UpdateDisplayNameAsync(name, req.DisplayName, ct));

    // PATCH /api/edge/types/{name}/props/{propName}/display-name（属性中文展示名）
    [HttpPatch("{name}/props/{propName}/display-name")]
    public async Task<IActionResult> UpdatePropDisplayName(
        string name, string propName,
        [FromBody] UpdateEdgePropDisplayNameRequest req, CancellationToken ct)
        => Ok(await svc.UpdatePropDisplayNameAsync(name, propName, req.DisplayName, ct));

    // DELETE /api/edge/types/{name}
    [HttpDelete("{name}")]
    public async Task<IActionResult> Delete(string name, CancellationToken ct)
    {
        await svc.DeleteAsync(name, ct);
        return Ok(true);
    }
}

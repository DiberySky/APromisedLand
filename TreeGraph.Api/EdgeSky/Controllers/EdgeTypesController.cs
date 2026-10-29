using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.EdgeSky.Services;
using TreeGraph.Shared.EdgeSky.Dtos;

namespace TreeGraph.Api.EdgeSky.Controllers;

/// <summary>
/// EdgeType 管理 API（/api/spaces/{spaceId}/edge/types）：镜像 NebulaGraph 边类型语义。
/// EdgeType 从属于图空间，空间内唯一、不可改名；TTL/注释仅创建时可设；
/// 变更仅限 ALTER ADD/DROP/CHANGE 属性。
/// 异常出口统一走 ApiEnvelopeFilter：400=参数/校验，404=不存在，409=业务规则冲突。
/// </summary>
[ApiController]
[Route("api/spaces/{spaceId}/edge/types")]
public class EdgeTypesController(EdgeTypeService svc) : ControllerBase
{
    // GET /api/spaces/{spaceId}/edge/types
    [HttpGet]
    public async Task<IActionResult> List(string spaceId, CancellationToken ct)
        => Ok(await svc.ListAsync(spaceId, ct));

    // GET /api/spaces/{spaceId}/edge/types/{name}
    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string spaceId, string name, CancellationToken ct)
        => Ok(await svc.GetAsync(spaceId, name, ct));

    // POST /api/spaces/{spaceId}/edge/types
    [HttpPost]
    public async Task<IActionResult> Create(string spaceId, [FromBody] CreateEdgeTypeRequest req, CancellationToken ct)
    {
        var dto = await svc.CreateAsync(spaceId, req, ct);
        return CreatedAtAction(nameof(Get), new { spaceId, name = dto.Name }, dto);
    }

    // PUT /api/spaces/{spaceId}/edge/types/{name}（ALTER：仅属性集增/删/改类型）
    [HttpPut("{name}")]
    public async Task<IActionResult> Alter(string spaceId, string name, [FromBody] AlterEdgeTypeRequest req, CancellationToken ct)
        => Ok(await svc.AlterAsync(spaceId, name, req, ct));

    // PATCH /api/spaces/{spaceId}/edge/types/{name}/display-name（非 Nebula 概念，仅更新中文展示名）
    [HttpPatch("{name}/display-name")]
    public async Task<IActionResult> UpdateDisplayName(
        string spaceId, string name, [FromBody] UpdateEdgeTypeDisplayNameRequest req, CancellationToken ct)
        => Ok(await svc.UpdateDisplayNameAsync(spaceId, name, req.DisplayName, ct));

    // PATCH /api/spaces/{spaceId}/edge/types/{name}/display-order（非 Nebula 概念，仅 UI 排序）
    [HttpPatch("{name}/display-order")]
    public async Task<IActionResult> UpdateDisplayOrder(
        string spaceId, string name, [FromBody] UpdateEdgeTypeDisplayOrderRequest req, CancellationToken ct)
        => Ok(await svc.UpdateDisplayOrderAsync(spaceId, name, req.DisplayOrder, ct));

    // PATCH /api/spaces/{spaceId}/edge/types/{name}/props/{propName}/display-name（属性中文展示名）
    [HttpPatch("{name}/props/{propName}/display-name")]
    public async Task<IActionResult> UpdatePropDisplayName(
        string spaceId, string name, string propName,
        [FromBody] UpdateEdgePropDisplayNameRequest req, CancellationToken ct)
        => Ok(await svc.UpdatePropDisplayNameAsync(spaceId, name, propName, req.DisplayName, ct));

    // DELETE /api/spaces/{spaceId}/edge/types/{name}
    [HttpDelete("{name}")]
    public async Task<IActionResult> Delete(string spaceId, string name, CancellationToken ct)
    {
        await svc.DeleteAsync(spaceId, name, ct);
        return Ok(true);
    }
}

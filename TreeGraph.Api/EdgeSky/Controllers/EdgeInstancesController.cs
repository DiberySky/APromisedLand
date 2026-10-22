using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.EdgeSky.Services;

namespace TreeGraph.Api.EdgeSky.Controllers;

/// <summary>
/// 全局边列表端点（跨边类型）。
///
/// 路由：GET /api/eav/edges?edgeTypeId=&amp;nodeIds=a,b,c&amp;search=
/// 与 <see cref="EdgeController"/>（api/eav/edges/{edgeTypeId}）互补：
/// 前者用于「边管理」独立页与线路结构视图，后者用于按类型 CRUD。
/// </summary>
[ApiController]
[Route("api/eav/edges")]
public class EdgeInstancesController : ControllerBase
{
    private readonly EdgeInstanceService _service;

    public EdgeInstancesController(EdgeInstanceService service) => _service = service;

    /// <summary>全局边列表；nodeIds 为逗号分隔，命中 From 或 To 即返回。</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? edgeTypeId = null,
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
            string.IsNullOrWhiteSpace(edgeTypeId) ? null : edgeTypeId,
            ids,
            string.IsNullOrWhiteSpace(search) ? null : search,
            ct);

        return Ok(list);
    }
}

using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.EdgeSky.Services;
using TreeGraph.Shared.EdgeSky.Dtos;

namespace TreeGraph.Api.EdgeSky.Controllers;

/// <summary>
/// 边实例 API（/api/edge/edges）：唯一标识 = 四元组 &lt;srcVid, edgeType, rank, dstVid&gt;。
/// PUT = 覆盖式 UPSERT（对齐 INSERT EDGE）；PATCH = 部分 SET（对齐 UPDATE EDGE）；
/// DELETE 按四元组定位（对齐 DELETE EDGE）。
/// 异常出口统一走 ApiEnvelopeFilter：400=参数/校验，404=不存在，409=业务规则冲突。
/// </summary>
[ApiController]
[Route("api/edge/edges")]
public class EdgesController(EdgeService svc) : ControllerBase
{
    // GET /api/edge/edges?edgeType=&srcVid=&dstVid=
    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] string? edgeType, [FromQuery] string? srcVid, [FromQuery] string? dstVid,
        CancellationToken ct)
        => Ok(await svc.QueryAsync(edgeType, srcVid, dstVid, ct));

    // GET /api/edge/edges/{edgeType}/{srcVid}/{rank}/{dstVid}
    [HttpGet("{edgeType}/{srcVid}/{rank:long}/{dstVid}")]
    public async Task<IActionResult> Get(
        string edgeType, string srcVid, long rank, string dstVid, CancellationToken ct)
        => Ok(await svc.GetAsync(edgeType, srcVid, rank, dstVid, ct));

    // PUT /api/edge/edges/{edgeType}/{srcVid}/{rank}/{dstVid}（覆盖式 UPSERT）
    [HttpPut("{edgeType}/{srcVid}/{rank:long}/{dstVid}")]
    public async Task<IActionResult> Upsert(
        string edgeType, string srcVid, long rank, string dstVid,
        [FromBody] UpsertEdgeRequest req, CancellationToken ct)
        => Ok(await svc.UpsertAsync(edgeType, srcVid, rank, dstVid, req, ct));

    // PATCH /api/edge/edges/{edgeType}/{srcVid}/{rank}/{dstVid}（部分 SET）
    [HttpPatch("{edgeType}/{srcVid}/{rank:long}/{dstVid}")]
    public async Task<IActionResult> Patch(
        string edgeType, string srcVid, long rank, string dstVid,
        [FromBody] PatchEdgeRequest req, CancellationToken ct)
        => Ok(await svc.PatchAsync(edgeType, srcVid, rank, dstVid, req, ct));

    // DELETE /api/edge/edges/{edgeType}/{srcVid}/{rank}/{dstVid}
    [HttpDelete("{edgeType}/{srcVid}/{rank:long}/{dstVid}")]
    public async Task<IActionResult> Delete(
        string edgeType, string srcVid, long rank, string dstVid, CancellationToken ct)
    {
        await svc.DeleteAsync(edgeType, srcVid, rank, dstVid, ct);
        return Ok(true);
    }
}

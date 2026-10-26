using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.EdgeSky.Services;
using TreeGraph.Shared.EdgeSky.Dtos;

namespace TreeGraph.Api.EdgeSky.Controllers;

/// <summary>
/// 边实例 API（/api/spaces/{spaceId}/edge/edges）：唯一标识 = 五元组
/// &lt;spaceId, edgeType, srcVid, rank, dstVid&gt;（边从属于图空间）。
/// PUT = 覆盖式 UPSERT（对齐 INSERT EDGE）；PATCH = 部分 SET（对齐 UPDATE EDGE）；
/// DELETE 按五元组定位（对齐 DELETE EDGE）。
/// 异常出口统一走 ApiEnvelopeFilter：400=参数/校验，404=不存在，409=业务规则冲突。
/// </summary>
[ApiController]
[Route("api/spaces/{spaceId}/edge/edges")]
public class EdgesController(EdgeService svc) : ControllerBase
{
    // GET /api/spaces/{spaceId}/edge/edges?edgeType=&srcVid=&dstVid=
    [HttpGet]
    public async Task<IActionResult> Query(
        string spaceId,
        [FromQuery] string? edgeType, [FromQuery] string? srcVid, [FromQuery] string? dstVid,
        CancellationToken ct)
        => Ok(await svc.QueryAsync(spaceId, edgeType, srcVid, dstVid, ct));

    // GET /api/spaces/{spaceId}/edge/edges/{edgeType}/{srcVid}/{rank}/{dstVid}
    [HttpGet("{edgeType}/{srcVid}/{rank:long}/{dstVid}")]
    public async Task<IActionResult> Get(
        string spaceId, string edgeType, string srcVid, long rank, string dstVid, CancellationToken ct)
        => Ok(await svc.GetAsync(spaceId, edgeType, srcVid, rank, dstVid, ct));

    // PUT /api/spaces/{spaceId}/edge/edges/{edgeType}/{srcVid}/{rank}/{dstVid}（覆盖式 UPSERT）
    [HttpPut("{edgeType}/{srcVid}/{rank:long}/{dstVid}")]
    public async Task<IActionResult> Upsert(
        string spaceId, string edgeType, string srcVid, long rank, string dstVid,
        [FromBody] UpsertEdgeRequest req, CancellationToken ct)
        => Ok(await svc.UpsertAsync(spaceId, edgeType, srcVid, rank, dstVid, req, ct));

    // PATCH /api/spaces/{spaceId}/edge/edges/{edgeType}/{srcVid}/{rank}/{dstVid}（部分 SET）
    [HttpPatch("{edgeType}/{srcVid}/{rank:long}/{dstVid}")]
    public async Task<IActionResult> Patch(
        string spaceId, string edgeType, string srcVid, long rank, string dstVid,
        [FromBody] PatchEdgeRequest req, CancellationToken ct)
        => Ok(await svc.PatchAsync(spaceId, edgeType, srcVid, rank, dstVid, req, ct));

    // DELETE /api/spaces/{spaceId}/edge/edges/{edgeType}/{srcVid}/{rank}/{dstVid}
    [HttpDelete("{edgeType}/{srcVid}/{rank:long}/{dstVid}")]
    public async Task<IActionResult> Delete(
        string spaceId, string edgeType, string srcVid, long rank, string dstVid, CancellationToken ct)
    {
        await svc.DeleteAsync(spaceId, edgeType, srcVid, rank, dstVid, ct);
        return Ok(true);
    }
}

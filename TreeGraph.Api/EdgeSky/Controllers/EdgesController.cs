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
    // GET /api/spaces/{spaceId}/edge/edges?edgeType=&srcVid=&dstVid=&nodeId=&direction=&limit=&offset=
    // direction=out|in|either（配合 nodeId 按起点/终点/双向过滤）
    [HttpGet]
    public async Task<IActionResult> Query(
        string spaceId,
        [FromQuery] string? edgeType, [FromQuery] string? srcVid, [FromQuery] string? dstVid,
        [FromQuery] string? nodeId, [FromQuery] string? direction,
        [FromQuery] int? limit, [FromQuery] int? offset,
        CancellationToken ct)
        => Ok(await svc.QueryAsync(spaceId, edgeType, srcVid, dstVid, nodeId, direction, limit, offset, ct));

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

/// <summary>
/// 节点 → 所属图空间解析（节点关系维护用）：GET /api/edge/node-space/{nodeId}。
/// 单独成控制器是因为 EdgesController 路由以 spaceId 为前缀，而此处入口尚不知空间。
/// </summary>
[ApiController]
[Route("api/edge/node-space")]
public class EdgeNodeSpaceController(EdgeService svc) : ControllerBase
{
    [HttpGet("{nodeId}")]
    public async Task<IActionResult> Get(string nodeId, CancellationToken ct)
    {
        var dto = await svc.GetNodeSpaceAsync(nodeId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }
}

/// <summary>对端节点候选搜索：GET /api/edge/nodes?search=。</summary>
[ApiController]
[Route("api/edge/nodes")]
public class EdgeNodeSearchController(EdgeService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? search, CancellationToken ct)
        => Ok(await svc.SearchNodesAsync(search, ct));
}

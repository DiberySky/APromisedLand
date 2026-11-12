using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.SpaceNodeSky.Services;
using TreeGraph.Shared.SpaceNodeSky.Dtos;

namespace TreeGraph.Api.SpaceNodeSky.Controllers;

/// <summary>
/// 空间节点（SpaceNode）CRUD 端点：api/spaces/{spaceId}/nodes。
///
/// 空间节点归属某个图空间，图同步只进入该空间。PostgreSQL 为唯一事实源，
/// 写操作成功后发布 InodeChangedEvent，由图/向量同步管道经数据总线异步收敛。
/// SpaceId 创建后不可改；跨空间移动 = 删除后重建。
/// </summary>
[ApiController]
[Route("api/spaces/{spaceId}/nodes")]
public class SpaceNodesController(SpaceNodeService svc) : ControllerBase
{
    /// <summary>空间内节点列表；可按名称（search）过滤。</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        string spaceId, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await svc.ListAsync(spaceId, search, ct));

    /// <summary>按 ID 查询（spaceId 不匹配视为不存在）。</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string spaceId, string id, CancellationToken ct = default)
    {
        var dto = await svc.GetAsync(spaceId, id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>创建空间节点。</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        string spaceId, [FromBody] CreateSpaceNodeRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("名称不能为空");

        var dto = await svc.CreateAsync(spaceId, req, ct);
        if (dto is null) return NotFound(new { error = "空间不存在" });

        return CreatedAtAction(nameof(GetById), new { spaceId, id = dto.Id }, dto);
    }

    /// <summary>更新空间节点（名称/描述；SpaceId 不可改）。</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string spaceId, string id, [FromBody] UpdateSpaceNodeRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("名称不能为空");

        var dto = await svc.UpdateAsync(spaceId, id, req, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>删除空间节点（仍有外部引用时 409；自身 EAV 数据级联清理）。</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string spaceId, string id, CancellationToken ct = default)
    {
        var result = await svc.DeleteAsync(spaceId, id, ct);
        if (!result.Found) return NotFound();
        if (result.ConflictError is not null) return Conflict(new { error = result.ConflictError });
        return NoContent();
    }
}

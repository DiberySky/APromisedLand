using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Shared.LineSky.Entities;

namespace TreeGraph.Api.LineSky.Controllers;

/// <summary>
/// 线路节点（LineNode）CRUD 端点。
///
/// LineNode 是独立一等公民实体，实现 Inode，不隶属于任何树。
/// 典型场景：输电线路杆塔、交叉跨越点、物流站点等。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LineNodeController : ControllerBase
{
    private readonly TreeGraphDbContext _db;

    public LineNodeController(TreeGraphDbContext db) => _db = db;

    /// <summary>查询全部 LineNode（支持按名称搜索）。</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search = null, CancellationToken ct = default)
    {
        var query = _db.LineNodes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(n => n.Name.Contains(search));
        var items = await query.OrderBy(n => n.Name).ToListAsync(ct);
        return Ok(items);
    }

    /// <summary>按 ID 查询单个 LineNode。</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct = default)
    {
        var node = await _db.LineNodes.FindAsync([id], ct);
        return node is null ? NotFound() : Ok(node);
    }

    /// <summary>创建 LineNode。</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LineNode node, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(node.Name))
            return BadRequest("名称不能为空");

        node.Id = string.IsNullOrWhiteSpace(node.Id) ? Guid.NewGuid().ToString() : node.Id;
        node.CreatedAt = DateTimeOffset.UtcNow;
        node.UpdatedAt = DateTimeOffset.UtcNow;

        _db.LineNodes.Add(node);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = node.Id }, node);
    }

    /// <summary>更新 LineNode。</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] LineNode node, CancellationToken ct = default)
    {
        var existing = await _db.LineNodes.FindAsync([id], ct);
        if (existing is null) return NotFound();

        existing.Name = node.Name;
        existing.Description = node.Description;
        existing.LocationGeo = node.LocationGeo;
        existing.IsArchived = node.IsArchived;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(existing);
    }

    /// <summary>删除 LineNode（有边引用时拒绝）。</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        var existing = await _db.LineNodes.FindAsync([id], ct);
        if (existing is null) return NotFound();

        var hasEdges = await _db.EdgeInstances
            .AnyAsync(e => e.FromNodeId == id || e.ToNodeId == id, ct);
        if (hasEdges)
            return Conflict("该节点仍有边引用，无法删除");

        _db.LineNodes.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

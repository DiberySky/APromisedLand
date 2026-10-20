using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Shared.LineSky.Entities;

namespace TreeGraph.Api.LineSky.Controllers;

/// <summary>
/// 线路（Line）CRUD 端点。
///
/// Line 是独立一等公民实体，实现 Inode，表达一条完整线路
/// （如输电线路、物流线路），与承载线路点的 LineNode 平级。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LinesController : ControllerBase
{
    private readonly TreeGraphDbContext _db;

    public LinesController(TreeGraphDbContext db) => _db = db;

    /// <summary>查询全部 Line（支持按名称搜索）。</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search = null, CancellationToken ct = default)
    {
        var query = _db.Lines.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(n => n.Name.Contains(search));
        var items = await query.OrderBy(n => n.Name).ToListAsync(ct);
        return Ok(items);
    }

    /// <summary>按 ID 查询单个 Line。</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct = default)
    {
        var line = await _db.Lines.FindAsync([id], ct);
        return line is null ? NotFound() : Ok(line);
    }

    /// <summary>创建 Line。</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Line line, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(line.Name))
            return BadRequest("名称不能为空");

        line.Id = string.IsNullOrWhiteSpace(line.Id) ? Guid.NewGuid().ToString() : line.Id;
        line.CreatedAt = DateTimeOffset.UtcNow;
        line.UpdatedAt = DateTimeOffset.UtcNow;

        _db.Lines.Add(line);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = line.Id }, line);
    }

    /// <summary>更新 Line。</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] Line line, CancellationToken ct = default)
    {
        var existing = await _db.Lines.FindAsync([id], ct);
        if (existing is null) return NotFound();

        existing.Name = line.Name;
        existing.Description = line.Description;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(existing);
    }

    /// <summary>删除 Line。</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        var existing = await _db.Lines.FindAsync([id], ct);
        if (existing is null) return NotFound();

        _db.Lines.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

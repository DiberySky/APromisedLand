using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Shared.LineSky.Entities;
using TreeGraph.Shared.Messaging;

namespace TreeGraph.Api.LineSky.Controllers;

/// <summary>
/// 线路（Line）CRUD 端点。
///
/// Line 是独立一等公民实体，实现 Inode，表达一条完整线路
/// （如输电线路、物流线路），与承载线路点的 LineNode 平级。
///
/// 写操作（创建/更新/删除）成功后发布 InodeChangedEvent，
/// 由向量同步与图同步管道的消费者即时刷新（消费端幂等）。
/// 线路改名设 PathAffected=true：其下全部线路节点的「所属线路」行失效，需级联重建。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LinesController : ControllerBase
{
    private readonly TreeGraphDbContext _db;
    private readonly IEventPublisher _publisher;

    public LinesController(TreeGraphDbContext db, IEventPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

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

        // ★ 线路新增 → 即时同步向量/图（消费端幂等）
        await PublishAsync(line.Id, InodeChangeKind.Saved, pathAffected: false, ct);

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

        // ★ 线路改名 → 其下全部线路节点的「所属线路」行失效，级联重建
        await PublishAsync(id, InodeChangeKind.Saved, pathAffected: true, ct);

        return Ok(existing);
    }

    /// <summary>删除 Line（其下仍有线路节点时拒绝，需先删除节点）。</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        var existing = await _db.Lines.FindAsync([id], ct);
        if (existing is null) return NotFound();

        // 线路节点以 line_id 外键（Restrict）挂在线上：仍有节点时拒绝，避免 23503 直抛 500
        var hasNodes = await _db.LineNodes.AnyAsync(n => n.LineId == id, ct);
        if (hasNodes)
            return Conflict(new { error = "该线路下仍有线路节点，请先删除节点后再删除线路" });

        // 先清空自身数据才允许删本项：线路仍有挂载实体数据时拒绝（不采用级联删除）
        var hasEntities = await _db.InodeEntities
            .AnyAsync(m => m.InodeId == id, ct);
        if (hasEntities)
            return Conflict(new { error = "该线路仍有挂载的实体数据，无法删除。请先清空其全部实体数据。" });

        // 类型声明（inode_entitytypes）不含业务数据，随宿主一并清理，避免孤儿声明
        var declarations = await _db.InodeEntityTypes
            .Where(x => x.InodeId == id)
            .ToListAsync(ct);
        _db.InodeEntityTypes.RemoveRange(declarations);

        _db.Lines.Remove(existing);
        await _db.SaveChangesAsync(ct);

        // ★ 线路已删除 → 消费端据 InodeId 清除残留向量/图顶点
        await PublishAsync(id, InodeChangeKind.Deleted, pathAffected: false, ct);

        return NoContent();
    }

    /// <summary>发布 iNode 变更事件（向量/图同步；at-least-once，消费端幂等）。</summary>
    private async Task PublishAsync(string inodeId, InodeChangeKind kind, bool pathAffected, CancellationToken ct)
    {
        await _publisher.Publish(new InodeChangedEvent
        {
            InodeId = inodeId,
            Kind = kind,
            PathAffected = pathAffected
        }, ct);
    }
}

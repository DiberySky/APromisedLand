using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Shared.LineSky.Entities;
using TreeGraph.Shared.Messaging;

namespace TreeGraph.Api.LineSky.Controllers;

/// <summary>
/// 线路节点（LineNode）CRUD 端点。
///
/// LineNode 是独立一等公民实体，实现 Inode，不隶属于任何树。
/// 典型场景：输电线路杆塔、交叉跨越点、物流站点等。
///
/// 写操作（创建/更新/删除）成功后发布 InodeChangedEvent，
/// 由向量同步与图同步管道的消费者即时刷新该节点（消费端幂等）。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LineNodeController : ControllerBase
{
    private readonly TreeGraphDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;

    public LineNodeController(TreeGraphDbContext db, IPublishEndpoint publishEndpoint)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
    }

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

        // ★ 节点新增 → 即时同步向量/图（消费端幂等）
        await PublishAsync(node.Id, InodeChangeKind.Saved, ct);

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
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        // ★ 节点更新 → 即时重建向量/图顶点
        await PublishAsync(id, InodeChangeKind.Saved, ct);

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

        // ★ 节点已删除 → 消费端据 InodeId 清除残留向量/图顶点
        await PublishAsync(id, InodeChangeKind.Deleted, ct);

        return NoContent();
    }

    /// <summary>发布 iNode 变更事件（向量/图同步；at-least-once，消费端幂等）。</summary>
    private async Task PublishAsync(string inodeId, InodeChangeKind kind, CancellationToken ct)
    {
        await _publishEndpoint.Publish(new InodeChangedEvent
        {
            InodeId = inodeId,
            Kind = kind
        }, ct);
    }
}

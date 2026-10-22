using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.MessagingSky;
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
    private readonly IEventPublisher _publishEndpoint;

    public LineNodeController(TreeGraphDbContext db, IEventPublisher publishEndpoint)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
    }

    /// <summary>查询 LineNode；可按所属线路（lineId）与名称（search）过滤。</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? lineId = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var query = _db.LineNodes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(lineId))
            query = query.Where(n => n.LineId == lineId);
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

        if (string.IsNullOrWhiteSpace(node.LineId))
            return BadRequest("所属线路不能为空");

        node.Id = string.IsNullOrWhiteSpace(node.Id) ? Guid.NewGuid().ToString() : node.Id;
        node.CreatedAt = DateTimeOffset.UtcNow;
        node.UpdatedAt = DateTimeOffset.UtcNow;

        _db.LineNodes.Add(node);
        await _db.SaveChangesAsync(ct);

        // ★ 节点新增 → 即时同步向量/图（消费端幂等）
        await PublishAsync(node.Id, InodeChangeKind.Saved, pathAffected: false, ct);

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
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        // ★ 节点更新 → 即时重建向量/图顶点（线路节点无后代，无需级联）
        await PublishAsync(id, InodeChangeKind.Saved, pathAffected: false, ct);

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
            return Conflict(new { error = "该节点仍有边引用，无法删除" });

        // 先清空自身数据才允许删本项：该节点仍有挂载实体数据时拒绝（不采用级联删除）
        var hasEntities = await _db.InodeEntities
            .AnyAsync(m => m.InodeId == id, ct);
        if (hasEntities)
            return Conflict(new { error = "该节点仍有挂载的实体数据，无法删除。请先清空其全部实体数据。" });

        // 类型声明（inode_entitytypes）不含业务数据，随宿主一并清理，避免孤儿声明
        var declarations = await _db.InodeEntityTypes
            .Where(x => x.InodeId == id)
            .ToListAsync(ct);
        _db.InodeEntityTypes.RemoveRange(declarations);

        _db.LineNodes.Remove(existing);
        await _db.SaveChangesAsync(ct);

        // ★ 节点已删除 → 消费端据 InodeId 清除残留向量/图顶点
        await PublishAsync(id, InodeChangeKind.Deleted, pathAffected: false, ct);

        return NoContent();
    }

    /// <summary>发布 iNode 变更事件（向量/图同步；at-least-once，消费端幂等）。
    /// 线路节点无后代，pathAffected 恒为 false（线路改名的级联由 LinesController 发起）。</summary>
    private async Task PublishAsync(string inodeId, InodeChangeKind kind, bool pathAffected, CancellationToken ct)
    {
        await _publishEndpoint.Publish(new InodeChangedEvent
        {
            InodeId = inodeId,
            Kind = kind,
            PathAffected = pathAffected
        }, ct);
    }
}

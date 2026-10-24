using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Shared.LineSky.Entities;
using TreeGraph.Shared.Messaging;
using TreeGraph.Shared.NodeSky.Entities;

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
        var query = _db.LineNodes.Include(n => n.Node).AsQueryable();
        if (!string.IsNullOrWhiteSpace(lineId))
            query = query.Where(n => n.LineId == lineId);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(n => n.Node!.Name.Contains(search));
        var items = await query.OrderBy(n => n.Node!.Name).ToListAsync(ct);
        return Ok(items);
    }

    /// <summary>按 ID 查询单个 LineNode。</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct = default)
    {
        var node = await _db.LineNodes
            .Include(n => n.Node)
            .FirstOrDefaultAsync(n => n.Id == id, ct);
        return node is null ? NotFound() : Ok(node);
    }

    /// <summary>创建 LineNode。</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LineNode node, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(node.Name))
            return BadRequest(new { error = "节点名称不能为空" });

        if (string.IsNullOrWhiteSpace(node.LineId))
            return BadRequest(new { error = "所属线路（lineId）不能为空" });

        var id = string.IsNullOrEmpty(node.Id) ? Guid.NewGuid().ToString() : node.Id;

        // 名称唯一真源：nodes 注册表（同事务，先注册表后宿主）
        _db.Nodes.Add(new Node
        {
            Id = id,
            HostType = "LineNode",
            Name = node.Name,
        });

        node.Id = id;
        node.CreatedAt = DateTimeOffset.UtcNow;
        node.UpdatedAt = null;
        await _db.LineNodes.AddAsync(node, ct);
        await _db.SaveChangesAsync(ct);

        await PublishAsync(id, InodeChangeKind.Saved, pathAffected: false, ct);

        return CreatedAtAction(nameof(GetById), new { id }, node);
    }

    /// <summary>更新 LineNode。</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] LineNode node, CancellationToken ct = default)
    {
        var existing = await _db.LineNodes
            .Include(n => n.Node)
            .FirstOrDefaultAsync(n => n.Id == id, ct);
        if (existing is null) return NotFound();

        if (existing.Node != null)
            existing.Node.Name = node.Name ?? existing.Node.Name;
        existing.Description = node.Description;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        await PublishAsync(id, InodeChangeKind.Saved, pathAffected: false, ct);

        return Ok(existing);
    }

    /// <summary>删除 LineNode（有边引用时拒绝）。</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        var existing = await _db.LineNodes.FindAsync([id], ct);
        if (existing is null) return NotFound();

        var hasRelations = await _db.RelationAttributeValues
            .AnyAsync(r => r.EntityId == id || r.ToNodeId == id, ct);
        if (hasRelations)
            return Conflict(new { error = "该节点仍被关系引用，无法删除" });

        var hasTerminal = await _db.LineTerminals
            .AnyAsync(t => t.NodeId == id, ct);
        if (hasTerminal)
            return Conflict(new { error = "该节点仍是线路结构的外部连接端子，无法删除。请先移除该端子。" });

        var hasEntities = await _db.InodeEntities
            .AnyAsync(m => m.InodeId == id, ct);
        if (hasEntities)
            return Conflict(new { error = "该节点仍有挂载的实体数据，无法删除。请先清空其全部实体数据。" });

        var declarations = await _db.InodeEntityTypes
            .Where(x => x.InodeId == id)
            .ToListAsync(ct);
        _db.InodeEntityTypes.RemoveRange(declarations);

        _db.LineNodes.Remove(existing);

        var nodeRow = await _db.Nodes.FindAsync([id], ct);
        if (nodeRow != null) _db.Nodes.Remove(nodeRow);

        await _db.SaveChangesAsync(ct);

        await PublishAsync(id, InodeChangeKind.Deleted, pathAffected: false, ct);

        return NoContent();
    }

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

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Shared.Messaging;
using TreeGraph.Shared.NodeSky.Entities;

namespace TreeGraph.Api.NodeSky.Controllers;

/// <summary>
/// 通用节点（Node）CRUD 端点。
///
/// Node 是无归属的一等公民实体，实现 Inode：无 ParentId / LineId 之类的外键归属，
/// 创建/销毁不依赖任何其他实体存活。只承载"确实无归属"的点（如独立设备、临时设施）。
///
/// 写操作（创建/更新/删除）成功后发布 InodeChangedEvent，
/// 由向量同步与图同步管道的消费者即时刷新该节点（消费端幂等）。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class NodeController : ControllerBase
{
    private readonly TreeGraphDbContext _db;
    private readonly IEventPublisher _publishEndpoint;

    public NodeController(TreeGraphDbContext db, IEventPublisher publishEndpoint)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
    }

    /// <summary>查询 Node；可按名称（search）过滤。</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var query = _db.Nodes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(n => n.Name != null && n.Name.Contains(search));
        var items = await query.OrderBy(n => n.Name).ToListAsync(ct);
        return Ok(items);
    }

    /// <summary>按 ID 查询单个 Node。</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct = default)
    {
        var node = await _db.Nodes.FindAsync([id], ct);
        return node is null ? NotFound() : Ok(node);
    }

    /// <summary>创建 Node。</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Node node, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(node.Name))
            return BadRequest("名称不能为空");

        node.Id = string.IsNullOrWhiteSpace(node.Id) ? Guid.NewGuid().ToString() : node.Id;
        // 一期 nodes 表即独立 Node 的宿主主表：host_type 恒为 "Node"，
        // 不接受调用方伪造（重构期各宿主才写入自己的类型名，spec §8.2）
        node.HostType = "Node";
        node.CreatedAt = DateTimeOffset.UtcNow;
        node.UpdatedAt = DateTimeOffset.UtcNow;

        _db.Nodes.Add(node);
        await _db.SaveChangesAsync(ct);

        // ★ 节点新增 → 即时同步向量/图（消费端幂等）
        await PublishAsync(node.Id, InodeChangeKind.Saved, pathAffected: false, ct);

        return CreatedAtAction(nameof(GetById), new { id = node.Id }, node);
    }

    /// <summary>更新 Node。</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] Node node, CancellationToken ct = default)
    {
        var existing = await _db.Nodes.FindAsync([id], ct);
        if (existing is null) return NotFound();

        if (string.IsNullOrWhiteSpace(node.Name))
            return BadRequest("名称不能为空");

        existing.Name = node.Name;
        existing.Description = node.Description;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        // ★ 节点更新 → 即时重建向量/图顶点（通用节点无后代，无需级联）
        await PublishAsync(id, InodeChangeKind.Saved, pathAffected: false, ct);

        return Ok(existing);
    }

    /// <summary>删除 Node（仍挂 EAV 属性值、关系或实体数据时拒绝；不做 force 级联，spec §4/§9.2）。</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        // Npgsql 执行策略不支持用户事务，需整体包裹
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            // 同一事务内先检测后删除，避免检测与删除之间插入新数据
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var existing = await _db.Nodes.FindAsync([id], ct);
            if (existing is null) return NotFound();

            // EAV 属性值（含位置坐标表）：仍挂业务数据时拒绝（不采用级联删除）
            var attrValueCount = await _db.AttributeValues.CountAsync(v => v.EntityId == id, ct);
            var locationCount = await _db.LocationAttributeValues.CountAsync(l => l.EntityId == id, ct);
            if (attrValueCount + locationCount > 0)
            {
                return Conflict(new
                {
                    error = $"该节点仍有 EAV 属性数据（{attrValueCount + locationCount} 项），无法删除。请先清空属性值。"
                });
            }

            // 边（edges）：作为起点
            var outgoingRelations = await _db.Edges
                .CountAsync(e => e.SrcVid == id, ct);
            if (outgoingRelations > 0)
            {
                return Conflict(new
                {
                    error = $"该节点仍有 {outgoingRelations} 条边（作为起点），无法删除。请先删除相关边。"
                });
            }

            // 边（edges）：作为终点被引用
            var incomingRelations = await _db.Edges
                .CountAsync(e => e.DstVid == id, ct);
            if (incomingRelations > 0)
            {
                return Conflict(new
                {
                    error = $"该节点仍被 {incomingRelations} 条边引用，无法删除。请先删除相关边。"
                });
            }

            // R7：端子引用（line_terminals.node_id FK 直指 nodes）
            var hasTerminal = await _db.LineTerminals.AnyAsync(t => t.NodeId == id, ct);
            if (hasTerminal)
            {
                return Conflict(new
                {
                    error = "该节点仍是线路结构的外部连接端子，无法删除。请先移除该端子。"
                });
            }

            // R7：被作为 parent（线路锚点 nodes.line_id）引用
            var referencedAsLine = await _db.Nodes.AnyAsync(n => n.LineId == id, ct);
            if (referencedAsLine)
            {
                return Conflict(new
                {
                    error = "该节点仍被作为线路锚点引用（存在挂到它的线路节点），无法删除。"
                });
            }

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

            _db.Nodes.Remove(existing);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // ★ 节点已删除 → 消费端据 InodeId 清除残留向量/图顶点
            await PublishAsync(id, InodeChangeKind.Deleted, pathAffected: false, ct);

            return NoContent();
        });
    }

    /// <summary>发布 iNode 变更事件（向量/图同步；at-least-once，消费端幂等）。
    /// 通用节点无后代，pathAffected 恒为 false。</summary>
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

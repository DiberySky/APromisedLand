using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Api.SpaceNodeSky.Entities;
using TreeGraph.Shared.Messaging;
using TreeGraph.Shared.NodeSky.Entities;
using TreeGraph.Shared.SpaceNodeSky.Dtos;

namespace TreeGraph.Api.SpaceNodeSky.Services;

/// <summary>删除结果：Found=false → 404；ConflictError 非空 → 409；二者皆空 → 成功。</summary>
public sealed record SpaceNodeDeleteResult(bool Found, string? ConflictError);

/// <summary>
/// 空间节点（SpaceNode）服务：PostgreSQL 为唯一事实源，不与 Nebula 直接交互；
/// 写操作（创建/更新/删除）成功后发布 InodeChangedEvent，图/向量经数据总线异步收敛（消费端幂等）。
/// SpaceId 创建后不可改，跨空间移动 = 删除后重建。
/// </summary>
public sealed class SpaceNodeService
{
    private readonly TreeGraphDbContext _db;
    private readonly IEventPublisher _publisher;

    public SpaceNodeService(TreeGraphDbContext db, IEventPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    /// <summary>空间内节点列表（可按名称过滤，按名称排序）。</summary>
    public async Task<List<SpaceNodeDto>> ListAsync(string spaceId, string? search, CancellationToken ct)
    {
        var q = from s in _db.SpaceNodes.AsNoTracking()
                join n in _db.Nodes.AsNoTracking() on s.Id equals n.Id
                where s.SpaceId == spaceId
                select new { s, n };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            q = q.Where(x => x.n.Name != null && x.n.Name.Contains(kw));
        }

        return await q.OrderBy(x => x.n.Name)
            .Select(x => new SpaceNodeDto
            {
                Id = x.s.Id,
                SpaceId = x.s.SpaceId,
                Name = x.n.Name,
                Description = x.n.Description,
                CreatedAt = x.s.CreatedAt,
                UpdatedAt = x.s.UpdatedAt
            })
            .ToListAsync(ct);
    }

    /// <summary>按 ID 查询（spaceId 不匹配视为不存在）。</summary>
    public async Task<SpaceNodeDto?> GetAsync(string spaceId, string id, CancellationToken ct)
    {
        return await (from s in _db.SpaceNodes.AsNoTracking()
                      join n in _db.Nodes.AsNoTracking() on s.Id equals n.Id
                      where s.Id == id && s.SpaceId == spaceId
                      select new SpaceNodeDto
                      {
                          Id = s.Id,
                          SpaceId = s.SpaceId,
                          Name = n.Name,
                          Description = n.Description,
                          CreatedAt = s.CreatedAt,
                          UpdatedAt = s.UpdatedAt
                      })
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>创建空间节点：nodes 身份行（host_type=SpaceNode）+ space_nodes 归属行同事务写入。空间不存在返回 null。</summary>
    public async Task<SpaceNodeDto?> CreateAsync(string spaceId, CreateSpaceNodeRequest req, CancellationToken ct)
    {
        var spaceExists = await _db.Spaces.AsNoTracking().AnyAsync(s => s.Id == spaceId, ct);
        if (!spaceExists) return null;

        var now = DateTimeOffset.UtcNow;
        var node = new Node
        {
            Id = Guid.NewGuid().ToString(),
            HostType = SpaceNode.HostTypeName,
            Name = req.Name.Trim(),
            Description = req.Description,
            CreatedAt = now,
            UpdatedAt = now
        };
        var host = new SpaceNode
        {
            Id = node.Id,
            SpaceId = spaceId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Nodes.Add(node);
        _db.SpaceNodes.Add(host);
        await _db.SaveChangesAsync(ct);

        // ★ 节点新增 → 即时同步向量/图（消费端幂等）
        await PublishAsync(node.Id, InodeChangeKind.Saved, ct);

        return new SpaceNodeDto
        {
            Id = host.Id,
            SpaceId = host.SpaceId,
            Name = node.Name,
            Description = node.Description,
            CreatedAt = host.CreatedAt,
            UpdatedAt = host.UpdatedAt
        };
    }

    /// <summary>更新名称/描述（写 nodes；SpaceId 不可改）。不存在或跨空间返回 null。</summary>
    public async Task<SpaceNodeDto?> UpdateAsync(string spaceId, string id, UpdateSpaceNodeRequest req, CancellationToken ct)
    {
        var host = await _db.SpaceNodes.FirstOrDefaultAsync(s => s.Id == id && s.SpaceId == spaceId, ct);
        if (host is null) return null;

        var node = await _db.Nodes.FindAsync([id], ct);
        if (node is null) return null;

        node.Name = req.Name.Trim();
        node.Description = req.Description;
        node.UpdatedAt = DateTimeOffset.UtcNow;
        host.UpdatedAt = node.UpdatedAt;

        await _db.SaveChangesAsync(ct);

        // ★ 节点更新 → 即时重建向量/图顶点（空间节点无后代，无需级联）
        await PublishAsync(id, InodeChangeKind.Saved, ct);

        return new SpaceNodeDto
        {
            Id = host.Id,
            SpaceId = host.SpaceId,
            Name = node.Name,
            Description = node.Description,
            CreatedAt = host.CreatedAt,
            UpdatedAt = host.UpdatedAt
        };
    }

    /// <summary>
    /// 删除空间节点。守卫链（评审 P0-5 决策）：
    /// 外部引用 409 —— 出/入边、线路端子、inode 挂载实体数据；
    /// 自身数据级联 —— EAV 属性值/位置值（非外部引用，文件孤儿由 OrphanedFileCleanupService 兜底）、类型声明。
    /// SpaceNode 不可能是线路锚点（nodes.line_id 仅指向 Line），省略锚点检查。
    /// </summary>
    public async Task<SpaceNodeDeleteResult> DeleteAsync(string spaceId, string id, CancellationToken ct)
    {
        // Npgsql 执行策略不支持用户事务，需整体包裹
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // 同一事务内先检测后删除，避免检测与删除之间插入新数据
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var host = await _db.SpaceNodes.FirstOrDefaultAsync(s => s.Id == id && s.SpaceId == spaceId, ct);
            if (host is null) return new SpaceNodeDeleteResult(false, null);
            var node = await _db.Nodes.FindAsync([id], ct);
            if (node is null) return new SpaceNodeDeleteResult(false, null);

            // 边（edges）：作为起点
            var outgoing = await _db.Edges.CountAsync(e => e.SrcVid == id, ct);
            if (outgoing > 0)
                return new SpaceNodeDeleteResult(true,
                    $"该节点仍有 {outgoing} 条边（作为起点），无法删除。请先删除相关边。");

            // 边（edges）：作为终点被引用
            var incoming = await _db.Edges.CountAsync(e => e.DstVid == id, ct);
            if (incoming > 0)
                return new SpaceNodeDeleteResult(true,
                    $"该节点仍被 {incoming} 条边引用，无法删除。请先删除相关边。");

            // 端子引用（line_terminals.node_id FK 直指 nodes）
            var hasTerminal = await _db.LineTerminals.AnyAsync(t => t.NodeId == id, ct);
            if (hasTerminal)
                return new SpaceNodeDeleteResult(true,
                    "该节点仍是线路结构的外部连接端子，无法删除。请先移除该端子。");

            // inode 挂载实体数据
            var hasEntities = await _db.InodeEntities.AnyAsync(m => m.InodeId == id, ct);
            if (hasEntities)
                return new SpaceNodeDeleteResult(true,
                    "该节点仍有挂载的实体数据，无法删除。请先清空其全部实体数据。");

            // EAV 属性值/位置值 = 节点自身数据（非外部引用）：级联删除，不拦截。
            // 文件型属性值的孤儿对象由 OrphanedFileCleanupService 兜底清理。
            var attrValues = await _db.AttributeValues.Where(v => v.EntityId == id).ToListAsync(ct);
            _db.AttributeValues.RemoveRange(attrValues);
            var locationValues = await _db.LocationAttributeValues.Where(l => l.EntityId == id).ToListAsync(ct);
            _db.LocationAttributeValues.RemoveRange(locationValues);

            // 类型声明（inode_entitytypes）不含业务数据，随宿主一并清理，避免孤儿声明
            var declarations = await _db.InodeEntityTypes.Where(x => x.InodeId == id).ToListAsync(ct);
            _db.InodeEntityTypes.RemoveRange(declarations);

            _db.SpaceNodes.Remove(host);
            _db.Nodes.Remove(node);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // ★ 节点已删除 → 消费端据 InodeId 清除残留向量/图顶点
            await PublishAsync(id, InodeChangeKind.Deleted, ct);

            return new SpaceNodeDeleteResult(true, null);
        });
    }

    /// <summary>发布 iNode 变更事件（图/向量同步；at-least-once，消费端幂等）。空间节点无后代，PathAffected 恒 false。</summary>
    private async Task PublishAsync(string inodeId, InodeChangeKind kind, CancellationToken ct)
        => await _publisher.Publish(new InodeChangedEvent
        {
            InodeId = inodeId,
            Kind = kind,
            PathAffected = false
        }, ct);
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Shared.Messaging;
using TreeGraph.Shared.NodeEavSky.Dtos;
using TreeGraph.Shared.NodeSky.Entities;

namespace TreeGraph.Api.LineSky.Controllers;

/// <summary>
/// 线路节点（LineNode）端点 —— R7 Facade：LineNode 收敛进 nodes 注册表。
///
/// 存储形态：
///   - 身份 = nodes 行（host_type='LineNode'，line_id = 所属 Line 的 nodes.id）；
///   - 结构连接 = edges 表 line_segment 边（rank 恒 0），from = 前驱节点、to = 自身；
///     首节点无边。
///
/// DTO 形状保持 LineNodeDto（Id / LineId / Name / Description + 创建用 PredecessorId）。
/// 写操作成功后发布 InodeChangedEvent（消费端幂等）；段边直写 edges 表并发布
/// EdgeChangedEvent 触发图同步。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LineNodeController : ControllerBase
{
    /// <summary>链边类型（edges.edge_type；seed 内置 EdgeType）。</summary>
    private const string SegmentEdgeType = "line_segment";

    private readonly TreeGraphDbContext _db;
    private readonly IEventPublisher _publishEndpoint;

    public LineNodeController(
        TreeGraphDbContext db, IEventPublisher publishEndpoint)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
    }

    /// <summary>
    /// 查询 LineNode；可按所属线路（lineId）与名称（search）过滤。
    /// 分页：pageSize > 0 时返回 PagedResult{Items,Total,Page,PageSize}；
    ///       pageSize = 0（默认）时返回全量 List（向后兼容既有调用方）。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? lineId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 0,
        CancellationToken ct = default)
    {
        var query = _db.Nodes.Where(n => n.HostType == "LineNode");
        if (!string.IsNullOrWhiteSpace(lineId))
            query = query.Where(n => n.LineId == lineId);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(n => n.Name != null && n.Name.Contains(search));

        var ordered = query.OrderBy(n => n.Name);

        if (pageSize > 0)
        {
            var total = await ordered.CountAsync(ct);
            var pageItems = await ordered
                .Skip(Math.Max(0, page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync(ct);
            var predMap = await GetPredecessorMapAsync(pageItems.Select(n => n.Id).ToList(), ct);
            var nameMap = pageItems.ToDictionary(n => n.Id, n => n.Name ?? "");
            var dtos = pageItems.Select(n =>
            {
                var hasPred = predMap.TryGetValue(n.Id, out var predId);
                return ToDto(n, hasPred ? predId : null, hasPred ? nameMap.GetValueOrDefault(predId!) : null);
            }).ToList();
            return Ok(new PagedResult<object>
            {
                Items = dtos.Cast<object>().ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            });
        }

        var items = await ordered.AsNoTracking().ToListAsync(ct);
        var pMap = await GetPredecessorMapAsync(items.Select(n => n.Id).ToList(), ct);
        var nMap = items.ToDictionary(n => n.Id, n => n.Name ?? "");
        return Ok(items.Select(n =>
        {
            var hasPred = pMap.TryGetValue(n.Id, out var predId);
            return ToDto(n, hasPred ? predId : null, hasPred ? nMap.GetValueOrDefault(predId!) : null);
        }).ToList());
    }

    /// <summary>按 ID 查询单个 LineNode。</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct = default)
    {
        var node = await _db.Nodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id && n.HostType == "LineNode", ct);
        if (node is null) return NotFound();

        var predMap = await GetPredecessorMapAsync(new[] { id }, ct);
        string? predName = null;
        if (predMap.TryGetValue(id, out var predId))
        {
            predName = await _db.Nodes.AsNoTracking()
                .Where(n => n.Id == predId)
                .Select(n => n.Name)
                .FirstOrDefaultAsync(ct);
        }
        return Ok(ToDto(node, predMap.GetValueOrDefault(id), predName));
    }

    /// <summary>
    /// 创建 LineNode：nodes 行（line_id=lineId，目标必须为 Line）
    /// → 有前驱则写 line_segment 边（前驱→自身，直写 edges 表）。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LineNodeCreateRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "节点名称不能为空" });
        if (string.IsNullOrWhiteSpace(req.LineId))
            return BadRequest(new { error = "所属线路（lineId）不能为空" });

        // line_id 目标必须是 Line（服务层校验，DB 不约束 host_type）
        var lineExists = await _db.Nodes
            .AnyAsync(n => n.Id == req.LineId && n.HostType == "Line", ct);
        if (!lineExists)
            return BadRequest(new { error = $"所属线路不存在或不是 Line: {req.LineId}" });

        // 段边写入线路所属空间（lines.space_id；lines 行缺失 = 数据异常）
        var spaceId = await LineSpaceIdAsync(req.LineId, ct);
        if (spaceId is null)
            return BadRequest(new { error = $"线路缺少所属空间（lines 行缺失）: {req.LineId}" });

        // 前驱校验：存在、同属本线
        if (!string.IsNullOrWhiteSpace(req.PredecessorId))
        {
            var pred = await _db.Nodes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == req.PredecessorId && n.HostType == "LineNode", ct);
            if (pred is null)
                return BadRequest(new { error = $"前驱节点不存在: {req.PredecessorId}" });
            if (pred.LineId != req.LineId)
                return BadRequest(new { error = "段边两端必须同属一条线路（前驱不在目标线路）" });
        }

        var id = string.IsNullOrEmpty(req.Id) ? Guid.NewGuid().ToString() : req.Id;
        var now = DateTimeOffset.UtcNow;

        // NpgsqlRetryingExecutionStrategy 不支持裸用户事务：经 CreateExecutionStrategy 包成可重试单元
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var node = new Node
            {
                Id = id,
                HostType = "LineNode",
                Name = req.Name,
                Description = req.Description,
                LineId = req.LineId,
                CreatedAt = now
            };
            _db.Nodes.Add(node);

            // 先落 nodes 行：段边 FK（edges.src_vid/dst_vid → nodes.id）要在同事务内查到本行
            await _db.SaveChangesAsync(ct);

            // 有前驱 → 写 line_segment 边（前驱→自身）；首节点无边
            var edgeEvents = new List<EdgeChangedEvent>();
            if (!string.IsNullOrWhiteSpace(req.PredecessorId))
            {
                UpsertSegmentEdge(spaceId, req.PredecessorId!, id, edgeEvents);
                await _db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);

            foreach (var evt in edgeEvents)
                await _publishEndpoint.Publish(evt, ct);
            await PublishAsync(id, InodeChangeKind.Saved, pathAffected: false, ct);

            return (IActionResult)CreatedAtAction(nameof(GetById), new { id }, ToDto(node, req.PredecessorId, null));
        });
    }

    /// <summary>
    /// 更新 LineNode：改 name/description；
    /// 提供了 PredecessorId 时重接段边（删旧前驱→本节点的 line_segment 边 + 写新前驱→本节点）；
    /// 换线路 = 改 line_id（连带校验段边两端仍同线，否则 400）。
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id, [FromBody] LineNodeUpdateRequest req, CancellationToken ct = default)
    {
        var node = await _db.Nodes
            .FirstOrDefaultAsync(n => n.Id == id && n.HostType == "LineNode", ct);
        if (node is null) return NotFound();

        var newLineId = string.IsNullOrWhiteSpace(req.LineId) ? node.LineId : req.LineId;

        // 段边写入线路所属空间（换线路或重接前驱时使用；lines 行缺失 = 数据异常）
        var spaceId = newLineId is null ? null : await LineSpaceIdAsync(newLineId, ct);

        // 换线路：目标必须是 Line
        if (newLineId != node.LineId)
        {
            var lineExists = await _db.Nodes
                .AnyAsync(n => n.Id == newLineId && n.HostType == "Line", ct);
            if (!lineExists)
                return BadRequest(new { error = $"所属线路不存在或不是 Line: {newLineId}" });
            if (spaceId is null)
                return BadRequest(new { error = $"线路缺少所属空间（lines 行缺失）: {newLineId}" });

            // 连带校验：段边两端仍同线（入边起点、出边终点都必须属于新线路）
            // 注：段边在旧线路空间内，此处不按空间过滤（节点 id 全局唯一，跨空间无碰撞）
            var endpointIds = await _db.Edges
                .Where(e => e.EdgeType == SegmentEdgeType
                         && (e.SrcVid == id || e.DstVid == id))
                .Select(e => e.SrcVid == id ? e.DstVid : e.SrcVid)
                .ToListAsync(ct);
            var alienCount = await _db.Nodes
                .CountAsync(n => endpointIds.Contains(n.Id) && n.LineId != newLineId, ct);
            if (alienCount > 0)
                return BadRequest(new
                {
                    error = "该节点仍有段边连接其他线路的节点，请先调整连接后再换线路"
                });
        }

        // 改连接：PredecessorId 非空 = 重接到该前驱
        if (!string.IsNullOrWhiteSpace(req.PredecessorId))
        {
            if (req.PredecessorId == id)
                return BadRequest(new { error = "段边不允许自环" });

            var pred = await _db.Nodes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == req.PredecessorId && n.HostType == "LineNode", ct);
            if (pred is null)
                return BadRequest(new { error = $"前驱节点不存在: {req.PredecessorId}" });
            if (pred.LineId != newLineId)
                return BadRequest(new { error = "段边两端必须同属一条线路（前驱不在目标线路）" });
        }

        node.Name = req.Name ?? node.Name;
        node.Description = req.Description;
        node.LineId = newLineId;
        node.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var edgeEvents = new List<EdgeChangedEvent>();
        if (!string.IsNullOrWhiteSpace(req.PredecessorId))
        {
            if (spaceId is null)
                return BadRequest(new { error = $"线路缺少所属空间（lines 行缺失）: {newLineId}" });

            // 当前入边前驱（若已是该前驱则无需重接；段边在本线路空间内）
            var currentPred = await _db.Edges
                .FirstOrDefaultAsync(e => e.SpaceId == spaceId
                    && e.EdgeType == SegmentEdgeType && e.DstVid == id, ct);

            if (currentPred?.SrcVid != req.PredecessorId)
            {
                // 删旧边（旧前驱 → 本节点），写新边（新前驱 → 自身）
                if (currentPred is not null)
                    DeleteSegmentEdge(currentPred, edgeEvents);
                UpsertSegmentEdge(spaceId, req.PredecessorId!, id, edgeEvents);
                await _db.SaveChangesAsync(ct);
            }
        }

        foreach (var evt in edgeEvents)
            await _publishEndpoint.Publish(evt, ct);
        await PublishAsync(id, InodeChangeKind.Saved, pathAffected: false, ct);

        return Ok(ToDto(node));
    }

    /// <summary>
    /// 删除 LineNode（守卫链：段边/边引用 → 端子引用 → 自身 EAV 值 → 挂载实体
    /// → 被作为 parent 引用 → 清声明 → 删 nodes 行）。
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        var node = await _db.Nodes
            .FirstOrDefaultAsync(n => n.Id == id && n.HostType == "LineNode", ct);
        if (node is null) return NotFound();

        // 段边/边引用（from 或 to；段边即 edges 表行）
        var hasEdges = await _db.Edges
            .AnyAsync(e => e.SrcVid == id || e.DstVid == id, ct);
        if (hasEdges)
            return Conflict(new { error = "该节点仍被边/段边引用，无法删除" });

        // 端子引用
        var hasTerminal = await _db.LineTerminals
            .AnyAsync(t => t.NodeId == id, ct);
        if (hasTerminal)
            return Conflict(new { error = "该节点仍是线路结构的外部连接端子，无法删除。请先移除该端子。" });

        // 自身 EAV 值
        var hasValues = await _db.AttributeValues.AnyAsync(v => v.EntityId == id, ct)
            || await _db.LocationAttributeValues.AnyAsync(l => l.EntityId == id, ct)
            || await _db.CustomTableRows.AnyAsync(r => r.ParentEntityId == id, ct);
        if (hasValues)
            return Conflict(new { error = "该节点仍有属性数据，无法删除。请先清空其全部属性数据。" });

        // 挂载实体
        var hasEntities = await _db.InodeEntities
            .AnyAsync(m => m.InodeId == id, ct);
        if (hasEntities)
            return Conflict(new { error = "该节点仍有挂载的实体数据，无法删除。请先清空其全部实体数据。" });

        // 被作为 parent 引用（nodes.line_id FK 兜底 + 预检）
        var referencedAsParent = await _db.Nodes.AnyAsync(n => n.LineId == id, ct);
        if (referencedAsParent)
            return Conflict(new { error = "该节点仍被作为线路锚点引用，无法删除" });

        // 类型声明不含业务数据，随宿主一并清理，避免孤儿声明
        var declarations = await _db.InodeEntityTypes
            .Where(x => x.InodeId == id)
            .ToListAsync(ct);
        _db.InodeEntityTypes.RemoveRange(declarations);

        _db.Nodes.Remove(node);
        await _db.SaveChangesAsync(ct);

        await PublishAsync(id, InodeChangeKind.Deleted, pathAffected: false, ct);

        return NoContent();
    }

    // ============================================================
    // 内部辅助
    // ============================================================

    private static object ToDto(Node n, string? predecessorId = null, string? predecessorName = null) => new
    {
        Id = n.Id,
        LineId = n.LineId ?? "",
        Name = n.Name ?? "",
        Description = n.Description,
        PredecessorId = predecessorId,
        PredecessorName = predecessorName,
        CreatedAt = n.CreatedAt
    };

    /// <summary>批量取前驱映射：LineNode id → 前驱节点 id（line_segment 边：from=前驱、to=自身）。</summary>
    private async Task<Dictionary<string, string>> GetPredecessorMapAsync(
        IReadOnlyCollection<string> nodeIds, CancellationToken ct)
    {
        if (nodeIds.Count == 0)
            return new Dictionary<string, string>();

        return await _db.Edges
            .Where(e => e.EdgeType == SegmentEdgeType && nodeIds.Contains(e.DstVid))
            .ToDictionaryAsync(e => e.DstVid, e => e.SrcVid, ct);
    }

    /// <summary>解析线路所属空间（lines.space_id）；lines 行缺失返回 null（调用方按上下文处置）。</summary>
    private async Task<string?> LineSpaceIdAsync(string lineId, CancellationToken ct)
        => await _db.Lines.AsNoTracking()
            .Where(l => l.Id == lineId)
            .Select(l => (string?)l.SpaceId)
            .FirstOrDefaultAsync(ct);

    /// <summary>upsert 一条 line_segment 段边（rank 恒 0，无 props；写入线路所属空间）。</summary>
    private void UpsertSegmentEdge(string spaceId, string fromId, string toId, List<EdgeChangedEvent> events)
    {
        _db.Edges.Add(new Edge
        {
            SpaceId = spaceId, EdgeType = SegmentEdgeType, SrcVid = fromId, Rank = 0, DstVid = toId
        });
        events.Add(new EdgeChangedEvent
        {
            SpaceId = spaceId, EdgeType = SegmentEdgeType, SrcVid = fromId, Rank = 0, DstVid = toId,
            Kind = EdgeChangeKind.Upsert
        });
    }

    /// <summary>删除一条已跟踪的段边。</summary>
    private void DeleteSegmentEdge(Edge edge, List<EdgeChangedEvent> events)
    {
        _db.Edges.Remove(edge);
        events.Add(new EdgeChangedEvent
        {
            SpaceId = edge.SpaceId, EdgeType = edge.EdgeType, SrcVid = edge.SrcVid,
            Rank = edge.Rank, DstVid = edge.DstVid,
            Kind = EdgeChangeKind.Delete
        });
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

/// <summary>创建 LineNode 请求（DTO 形状 = LineNodeDto + 可选前驱）。</summary>
public class LineNodeCreateRequest
{
    public string? Id { get; set; }
    public string LineId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>前驱节点 Id（同线 LineNode；空 = 首节点，无段边）。</summary>
    public string? PredecessorId { get; set; }
}

/// <summary>更新 LineNode 请求。</summary>
public class LineNodeUpdateRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }

    /// <summary>换线路（空 = 不变）；连带校验段边两端仍同线。</summary>
    public string? LineId { get; set; }

    /// <summary>重接前驱（空 = 不变）；会删旧前驱的 next 边并写新边。</summary>
    public string? PredecessorId { get; set; }
}

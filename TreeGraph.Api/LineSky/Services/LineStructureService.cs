using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.LineSky.Entities;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.LineSky.Dtos;
using TreeGraph.Shared.LineSky.Entities;
using TreeGraph.Shared.Messaging;
using TreeGraph.Shared.NodeSky.Entities;

namespace TreeGraph.Api.LineSky.Services;

/// <summary>
/// 线路结构服务 —— R7 边集薄层：
///   节点集 = nodes where line_id=lineId（host_type='LineNode'）；
///   边集 = 线路所属空间（lines.space_id）内、两端均属本线的 edges 记录
///         （一条记录 = 一条有向边，line_segment 为链边）；
///   链序 = 沿 line_segment 链走（无入边者为起点；分叉/环按发现序）。
///
/// 拓扑模式校验整体删除；只保留：禁自环、同线校验、诊断（连通/成环/度数，只报告不阻断）。
/// 端子校验只留「投运 FeedIn ≤ 1」。段 Weight 已砍（DisplayName → props.name）。
/// 段边直写 edges 表（EdgeSky：space_id=线路空间，edge_type='line_segment'，rank 恒 0，props={"name":...}），
/// 写成功后发布 EdgeChangedEvent 触发图同步。
/// </summary>
public class LineStructureService
{
    /// <summary>段边在 edges 表中的属性键：显示名。</summary>
    private const string NameProp = "name";

    /// <summary>链边类型（edges.edge_type；seed 内置 EdgeType）。</summary>
    private const string SegmentEdgeType = "line_segment";

    private readonly TreeGraphDbContext _db;
    private readonly InodeOwnerGuardRegistry _ownerGuards;
    private readonly IEventPublisher _publisher;

    public LineStructureService(
        TreeGraphDbContext db,
        InodeOwnerGuardRegistry ownerGuards,
        IEventPublisher publisher)
    {
        _db = db;
        _ownerGuards = ownerGuards;
        _publisher = publisher;
    }

    // ============================================================
    // 读取
    // ============================================================

    /// <summary>读取线路结构视图；线路不存在返回 null。</summary>
    public async Task<LineStructureDto?> GetAsync(string lineId, CancellationToken ct)
    {
        var line = await _db.Nodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == lineId && n.HostType == "Line", ct);
        if (line is null) return null;

        var spaceId = await LineSpaceIdAsync(lineId, ct);

        var nodes = await _db.Nodes.AsNoTracking()
            .Where(n => n.HostType == "LineNode" && n.LineId == lineId)
            .OrderBy(n => n.Name)
            .ToListAsync(ct);
        var nodeIds = nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);

        // 边集 = 线路所属空间内、两端均属本线的 edges 记录（含 line_segment 与用户自定义分支边）
        var edges = nodeIds.Count == 0 || spaceId is null
            ? new List<Edge>()
            : await _db.Edges.AsNoTracking()
                .Where(e => e.SpaceId == spaceId && nodeIds.Contains(e.SrcVid) && nodeIds.Contains(e.DstVid))
                .ToListAsync(ct);

        var terminals = await _db.LineTerminals.AsNoTracking()
            .Where(t => t.LineId == lineId)
            .ToListAsync(ct);

        // 外部连接节点显示名（能解析到时填充）
        var externalIds = terminals
            .Where(t => !string.IsNullOrWhiteSpace(t.ExternalNodeId))
            .Select(t => t.ExternalNodeId!)
            .ToList();
        var externalNames = externalIds.Count == 0
            ? new Dictionary<string, string>()
            : await _ownerGuards.ResolveNodeNamesAsync(externalIds, ct);

        var nameMap = nodes.ToDictionary(n => n.Id, n => n.Name ?? "", StringComparer.Ordinal);
        var terminalMap = terminals.ToDictionary(t => t.NodeId, StringComparer.Ordinal);

        LineStructureNodeDto ToNodeDto(Node n)
        {
            terminalMap.TryGetValue(n.Id, out var t);
            return new LineStructureNodeDto
            {
                NodeId = n.Id,
                Name = n.Name ?? "",
                TerminalRole = t?.Role,
                OperatingState = t?.OperatingState,
                ExternalNodeId = t?.ExternalNodeId,
                ExternalNodeName = t?.ExternalNodeId is { } ext
                    && externalNames.TryGetValue(ext, out var extName) ? extName : null
            };
        }

        var segmentEdges = edges
            .Where(e => e.EdgeType == SegmentEdgeType)
            .ToList();

        return new LineStructureDto
        {
            Exists = nodes.Count > 0,
            LineId = lineId,
            LineName = line.Name ?? "",
            UpdatedAt = nodes.Select(n => n.UpdatedAt ?? (DateTimeOffset?)n.CreatedAt)
                .Concat(edges.Select(e => (DateTimeOffset?)e.UpdatedAt))
                .DefaultIfEmpty()
                .Max(),
            OrderedNodeIds = DeriveOrder(nodes, segmentEdges),
            Nodes = nodes.Select(ToNodeDto).ToList(),
            Segments = edges.Select(e => new LineStructureSegmentDto
            {
                EdgeId = EncodeEdgeId(e),
                FromNodeId = e.SrcVid,
                FromNodeName = nameMap.GetValueOrDefault(e.SrcVid, ""),
                ToNodeId = e.DstVid,
                ToNodeName = nameMap.GetValueOrDefault(e.DstVid, ""),
                EdgeType = e.EdgeType,
                DisplayName = EdgeName(e)
            }).ToList(),
            Terminals = terminals.Select(t => new LineStructureNodeDto
            {
                NodeId = t.NodeId,
                Name = nameMap.GetValueOrDefault(t.NodeId, ""),
                TerminalRole = t.Role,
                OperatingState = t.OperatingState,
                ExternalNodeId = t.ExternalNodeId,
                ExternalNodeName = t.ExternalNodeId is { } ext
                    && externalNames.TryGetValue(ext, out var extName) ? extName : null
            }).ToList(),
            Diagnostics = Diagnose(nodes, edges, terminals)
        };
    }

    // ============================================================
    // 建/替换（Segments 全量替换 line_segment 边集；Terminals 全量替换端子集）
    // ============================================================

    /// <summary>创建结构：已有段边时 409；否则等价于 Replace。</summary>
    public async Task<LineStructureDto?> CreateAsync(
        string lineId, LineStructureRequest req, CancellationToken ct)
    {
        if (!await LineExistsAsync(lineId, ct)) return null;
        var spaceId = await LineSpaceIdAsync(lineId, ct)
            ?? throw new InvalidOperationException($"线路 {lineId} 缺少所属空间（lines 行缺失）");

        var nodeIds = await LineNodeIdsAsync(lineId, ct);
        var hasEdges = nodeIds.Count > 0
            && await _db.Edges.AnyAsync(
                e => e.SpaceId == spaceId && e.EdgeType == SegmentEdgeType
                  && nodeIds.Contains(e.SrcVid) && nodeIds.Contains(e.DstVid), ct);
        if (hasEdges)
            throw new InvalidOperationException("该线路已存在结构段边，请使用替换或逐段调整");

        return await ReplaceAsync(lineId, req, ct);
    }

    /// <summary>全量替换：段边集 + 端子集（同事务；段边直写 edges 表，提交后发事件）。</summary>
    public async Task<LineStructureDto?> ReplaceAsync(
        string lineId, LineStructureRequest req, CancellationToken ct)
    {
        if (!await LineExistsAsync(lineId, ct)) return null;
        var spaceId = await LineSpaceIdAsync(lineId, ct)
            ?? throw new InvalidOperationException($"线路 {lineId} 缺少所属空间（lines 行缺失）");

        var nodeIds = await LineNodeIdsAsync(lineId, ct);
        var desired = ValidateSegments(req.Segments, nodeIds);

        // NpgsqlRetryingExecutionStrategy 不支持裸用户事务：经 CreateExecutionStrategy 包成可重试单元
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var events = new List<EdgeChangedEvent>();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // 1. 段边全量替换（仅 line_segment；用户自定义分支边不动）
            var existing = nodeIds.Count == 0
                ? new List<Edge>()
                : await _db.Edges
                    .Where(e => e.SpaceId == spaceId && e.EdgeType == SegmentEdgeType
                             && nodeIds.Contains(e.SrcVid) && nodeIds.Contains(e.DstVid))
                    .ToListAsync(ct);

            var desiredSet = desired
                .Select(d => (d.FromNodeId, d.ToNodeId))
                .ToHashSet();

            // 先删不需要的旧边（释放 from 的单出边占用）
            foreach (var e in existing.Where(e => !desiredSet.Contains((e.SrcVid, e.DstVid))))
                DeleteSegmentEdge(e, events);

            // 再写新边（已存在且一致的自然跳过；DisplayName 变化也覆盖更新）
            var existingSet = existing
                .Select(e => (e.SrcVid, e.DstVid))
                .ToHashSet();
            foreach (var d in desired.Where(d => !existingSet.Contains((d.FromNodeId, d.ToNodeId))))
                UpsertSegmentEdge(spaceId, d.FromNodeId, d.ToNodeId, d.DisplayName, existing, events);

            // 2. 端子全量替换（null = 清空；PUT = 全量替换语义，与「清空结构」一致）
            await ReplaceTerminalsAsync(lineId, nodeIds, req.Terminals ?? [], ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // 事务提交后发布图同步事件（消费端回查 PG 读已提交状态）
            foreach (var evt in events)
                await _publisher.Publish(evt, ct);

            return await GetAsync(lineId, ct);
        });
    }

    // ============================================================
    // 单段加/删
    // ============================================================

    /// <summary>新增一段（写 edges 表 line_segment 边，rank 恒 0；节点须同属该线）。</summary>
    public async Task<LineStructureDto> AddSegmentAsync(
        string lineId, AddLineSegmentRequest req, CancellationToken ct)
    {
        if (!await LineExistsAsync(lineId, ct))
            throw new KeyNotFoundException($"线路不存在: {lineId}");
        var spaceId = await LineSpaceIdAsync(lineId, ct)
            ?? throw new InvalidOperationException($"线路 {lineId} 缺少所属空间（lines 行缺失）");

        var nodeIds = await LineNodeIdsAsync(lineId, ct);
        ValidateSegmentEndpoints(req.FromNodeId, req.ToNodeId, nodeIds);

        var events = new List<EdgeChangedEvent>();
        // 单出边语义：同一 from 的旧 line_segment 出边先删（幂等覆盖）
        var stale = await _db.Edges
            .Where(e => e.SpaceId == spaceId && e.EdgeType == SegmentEdgeType && e.SrcVid == req.FromNodeId)
            .ToListAsync(ct);
        foreach (var s in stale.Where(s => s.DstVid != req.ToNodeId))
            DeleteSegmentEdge(s, events);

        var existing = await _db.Edges
            .Where(e => e.SpaceId == spaceId && e.EdgeType == SegmentEdgeType && nodeIds.Contains(e.SrcVid))
            .ToListAsync(ct);
        UpsertSegmentEdge(spaceId, req.FromNodeId, req.ToNodeId, req.DisplayName, existing, events);

        await _db.SaveChangesAsync(ct);
        foreach (var evt in events)
            await _publisher.Publish(evt, ct);

        return (await GetAsync(lineId, ct))!;
    }

    /// <summary>删除一段（按四元组编码 edgeId；空间由线路解析；两端均属本线才可删）。</summary>
    public async Task<LineStructureDto> DeleteSegmentAsync(
        string lineId, string edgeId, CancellationToken ct)
    {
        var (edgeType, srcVid, rank, dstVid) = DecodeEdgeId(edgeId);
        var spaceId = await LineSpaceIdAsync(lineId, ct);

        var edge = spaceId is null
            ? null
            : await _db.Edges.FindAsync([spaceId, edgeType, srcVid, rank, dstVid], ct);
        if (edge is null)
            throw new KeyNotFoundException($"段边不存在: {edgeId}");

        var nodeIds = await LineNodeIdsAsync(lineId, ct);
        if (!nodeIds.Contains(edge.SrcVid) || !nodeIds.Contains(edge.DstVid))
            throw new ArgumentException($"段边 {edgeId} 不属于线路 {lineId}");

        var events = new List<EdgeChangedEvent>();
        DeleteSegmentEdge(edge, events);
        await _db.SaveChangesAsync(ct);
        foreach (var evt in events)
            await _publisher.Publish(evt, ct);

        return (await GetAsync(lineId, ct))!;
    }

    // ============================================================
    // 端子（校验只留「投运 FeedIn ≤ 1」）
    // ============================================================

    /// <summary>新增/更新端子（按 (lineId, nodeId) upsert）。</summary>
    public async Task<LineStructureDto> UpsertTerminalAsync(
        string lineId, AddLineTerminalRequest req, CancellationToken ct)
    {
        if (!await LineExistsAsync(lineId, ct))
            throw new KeyNotFoundException($"线路不存在: {lineId}");

        var nodeIds = await LineNodeIdsAsync(lineId, ct);
        if (!nodeIds.Contains(req.NodeId))
            throw new ArgumentException($"端子节点 {req.NodeId} 不属于线路 {lineId}");

        if (!string.IsNullOrWhiteSpace(req.ExternalNodeId))
            await _ownerGuards.EnsureNodeExistsAsync(req.ExternalNodeId, ct);

        var terminals = await _db.LineTerminals
            .Where(t => t.LineId == lineId)
            .ToListAsync(ct);

        var existing = terminals.FirstOrDefault(t => t.NodeId == req.NodeId);
        if (existing is null)
        {
            _db.LineTerminals.Add(new LineTerminal
            {
                LineId = lineId,
                NodeId = req.NodeId,
                Role = req.Role,
                OperatingState = req.OperatingState,
                ExternalNodeId = req.ExternalNodeId
            });
        }
        else
        {
            existing.Role = req.Role;
            existing.OperatingState = req.OperatingState;
            existing.ExternalNodeId = req.ExternalNodeId;
        }

        // 多电源校验：同一时刻投运的 FeedIn 最多 1 个
        var energizedFeedIn = terminals
            .Where(t => t.NodeId != req.NodeId)
            .Count(t => t.Role == TerminalRole.FeedIn
                     && t.OperatingState == TerminalOperatingState.Energized)
            + (req.Role == TerminalRole.FeedIn
                     && req.OperatingState == TerminalOperatingState.Energized ? 1 : 0);
        if (energizedFeedIn > 1)
            throw new ArgumentException($"同时投运的 FeedIn 端子最多 1 个（当前将有 {energizedFeedIn} 个投运）");

        await _db.SaveChangesAsync(ct);
        return (await GetAsync(lineId, ct))!;
    }

    /// <summary>移除端子。</summary>
    public async Task<LineStructureDto> DeleteTerminalAsync(
        string lineId, string nodeId, CancellationToken ct)
    {
        if (!await LineExistsAsync(lineId, ct))
            throw new KeyNotFoundException($"线路不存在: {lineId}");

        var terminal = await _db.LineTerminals
            .FirstOrDefaultAsync(t => t.LineId == lineId && t.NodeId == nodeId, ct);
        if (terminal is not null)
        {
            _db.LineTerminals.Remove(terminal);
            await _db.SaveChangesAsync(ct);
        }

        return (await GetAsync(lineId, ct))!;
    }

    // ============================================================
    // 内部辅助
    // ============================================================

    private async Task<bool> LineExistsAsync(string lineId, CancellationToken ct)
        => await _db.Nodes.AnyAsync(n => n.Id == lineId && n.HostType == "Line", ct);

    /// <summary>解析线路所属空间（lines.space_id）；lines 行缺失返回 null（调用方按上下文处置）。</summary>
    private async Task<string?> LineSpaceIdAsync(string lineId, CancellationToken ct)
        => await _db.Lines.AsNoTracking()
            .Where(l => l.Id == lineId)
            .Select(l => (string?)l.SpaceId)
            .FirstOrDefaultAsync(ct);

    private async Task<HashSet<string>> LineNodeIdsAsync(string lineId, CancellationToken ct)
        => (await _db.Nodes.AsNoTracking()
                .Where(n => n.HostType == "LineNode" && n.LineId == lineId)
                .Select(n => n.Id)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

    // ------------------------------------------------------------
    // 段边读写（edges 表，直写；事件收集后由调用方在提交后发布）
    // ------------------------------------------------------------

    /// <summary>upsert 一条 line_segment 段边（覆盖语义：props={"name":displayName}；spaceId = 线路所属空间）。</summary>
    private void UpsertSegmentEdge(
        string spaceId, string fromId, string toId, string? displayName,
        List<Edge> tracked, List<EdgeChangedEvent> events)
    {
        var edge = tracked.FirstOrDefault(
            e => e.SpaceId == spaceId && e.EdgeType == SegmentEdgeType && e.SrcVid == fromId
                 && e.Rank == 0 && e.DstVid == toId);
        if (edge is null)
        {
            edge = new Edge { SpaceId = spaceId, EdgeType = SegmentEdgeType, SrcVid = fromId, Rank = 0, DstVid = toId };
            _db.Edges.Add(edge);
            tracked.Add(edge);
        }

        edge.Props = string.IsNullOrWhiteSpace(displayName)
            ? null
            : JsonSerializer.SerializeToElement(
                new Dictionary<string, string> { [NameProp] = displayName });
        edge.UpdatedAt = DateTimeOffset.UtcNow;

        events.Add(new EdgeChangedEvent
        {
            SpaceId = edge.SpaceId,
            EdgeType = edge.EdgeType, SrcVid = edge.SrcVid,
            Rank = edge.Rank, DstVid = edge.DstVid,
            Kind = EdgeChangeKind.Upsert
        });
    }

    /// <summary>删除一条段边（任何 edge_type；RemoveRange 前已加载的跟踪实体）。</summary>
    private void DeleteSegmentEdge(Edge edge, List<EdgeChangedEvent> events)
    {
        _db.Edges.Remove(edge);
        events.Add(new EdgeChangedEvent
        {
            SpaceId = edge.SpaceId,
            EdgeType = edge.EdgeType, SrcVid = edge.SrcVid,
            Rank = edge.Rank, DstVid = edge.DstVid,
            Kind = EdgeChangeKind.Delete
        });
    }

    /// <summary>段边显示名 = props.name（缺失/非字符串 → null）。</summary>
    private static string? EdgeName(Edge e)
        => e.Props is { } p
           && p.TryGetProperty(NameProp, out var n)
           && n.ValueKind == JsonValueKind.String
            ? n.GetString() : null;

    /// <summary>边实例 Id = 四元组编码 "{edgeType}:{srcVid}:{rank}:{dstVid}"（各段均不含冒号）。</summary>
    private static string EncodeEdgeId(Edge e)
        => $"{e.EdgeType}:{e.SrcVid}:{e.Rank}:{e.DstVid}";

    /// <summary>解析四元组编码 edgeId；格式非法 → ArgumentException。</summary>
    private static (string EdgeType, string SrcVid, long Rank, string DstVid) DecodeEdgeId(string edgeId)
    {
        var parts = (edgeId ?? "").Split(':');
        if (parts.Length != 4 || string.IsNullOrWhiteSpace(parts[0])
            || string.IsNullOrWhiteSpace(parts[1])
            || !long.TryParse(parts[2], out var rank)
            || string.IsNullOrWhiteSpace(parts[3]))
            throw new ArgumentException($"段边 Id 格式非法（期望 edgeType:srcVid:rank:dstVid）: {edgeId}");
        return (parts[0], parts[1], rank, parts[3]);
    }

    private static void ValidateSegmentEndpoints(
        string fromId, string toId, HashSet<string> nodeIds)
    {
        if (fromId == toId)
            throw new ArgumentException("段边不允许自环（from 与 to 相同）");
        if (!nodeIds.Contains(fromId) || !nodeIds.Contains(toId))
            throw new ArgumentException("段边两端必须同属一条线路");
    }

    /// <summary>校验段输入集合并去重；单值语义：同一 from 只允许一条出边。</summary>
    private static List<LineStructureSegmentInput> ValidateSegments(
        List<LineStructureSegmentInput>? segments, HashSet<string> nodeIds)
    {
        var result = new List<LineStructureSegmentInput>();
        if (segments is null) return result;

        var seen = new HashSet<(string, string)>();
        var fromUsed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in segments)
        {
            ValidateSegmentEndpoints(s.FromNodeId, s.ToNodeId, nodeIds);
            if (!seen.Add((s.FromNodeId, s.ToNodeId))) continue; // 幂等去重
            if (!fromUsed.Add(s.FromNodeId))
                throw new ArgumentException(
                    $"节点 {s.FromNodeId} 有多条出边：line_segment 为单出边语义，分支请建自定义边类型");
            result.Add(s);
        }
        return result;
    }

    private async Task ReplaceTerminalsAsync(
        string lineId, HashSet<string> nodeIds,
        List<LineStructureTerminalInput> terminals, CancellationToken ct)
    {
        foreach (var t in terminals)
        {
            if (!nodeIds.Contains(t.NodeId))
                throw new ArgumentException($"端子节点 {t.NodeId} 不属于线路 {lineId}");
            if (!string.IsNullOrWhiteSpace(t.ExternalNodeId))
                await _ownerGuards.EnsureNodeExistsAsync(t.ExternalNodeId, ct);
        }

        var energizedFeedIn = terminals.Count(t =>
            t.Role == TerminalRole.FeedIn && t.OperatingState == TerminalOperatingState.Energized);
        if (energizedFeedIn > 1)
            throw new ArgumentException($"同时投运的 FeedIn 端子最多 1 个（当前有 {energizedFeedIn} 个投运）");

        var existing = await _db.LineTerminals.Where(t => t.LineId == lineId).ToListAsync(ct);
        _db.LineTerminals.RemoveRange(existing);
        foreach (var t in terminals.DistinctBy(t => t.NodeId))
        {
            _db.LineTerminals.Add(new LineTerminal
            {
                LineId = lineId,
                NodeId = t.NodeId,
                Role = t.Role,
                OperatingState = t.OperatingState,
                ExternalNodeId = t.ExternalNodeId
            });
        }
    }

    /// <summary>沿 line_segment 链推导节点顺序（无入边者为起点；分叉/环按发现序）。</summary>
    private static List<string> DeriveOrder(
        List<Node> nodes,
        List<Edge> segmentEdges)
    {
        if (nodes.Count == 0) return new List<string>();

        var outgoing = segmentEdges
            .GroupBy(e => e.SrcVid)
            .ToDictionary(g => g.Key, g => g.Select(e => e.DstVid).ToList(), StringComparer.Ordinal);
        var hasIncoming = segmentEdges.Select(e => e.DstVid).ToHashSet(StringComparer.Ordinal);

        var ordered = new List<string>(nodes.Count);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        // 起点 = 无入边节点（按名称序）；分叉按发现序（BFS）
        var queue = new Queue<string>(
            nodes.Where(n => !hasIncoming.Contains(n.Id)).Select(n => n.Id));
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!visited.Add(id)) continue;
            ordered.Add(id);
            if (outgoing.TryGetValue(id, out var nexts))
                foreach (var next in nexts) queue.Enqueue(next);
        }

        // 环内无入边起点遗漏的节点：按发现序补上
        foreach (var n in nodes)
        {
            if (visited.Add(n.Id))
            {
                ordered.Add(n.Id);
                if (outgoing.TryGetValue(n.Id, out var nexts))
                    foreach (var next in nexts)
                        if (!visited.Contains(next)) queue.Enqueue(next);
                while (queue.Count > 0)
                {
                    var id = queue.Dequeue();
                    if (!visited.Add(id)) continue;
                    ordered.Add(id);
                    if (outgoing.TryGetValue(id, out var nx))
                        foreach (var next in nx) queue.Enqueue(next);
                }
            }
        }

        return ordered;
    }

    /// <summary>结构诊断：连通/成环/度数/端子统计（只报告不阻断；投运 FeedIn&gt;1 才记错误）。</summary>
    private static LineStructureDiagnosticsDto Diagnose(
        List<Node> nodes,
        List<Edge> edges,
        List<LineTerminal> terminals)
    {
        var nodeIds = nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var degree = nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
        foreach (var e in edges)
        {
            if (degree.ContainsKey(e.SrcVid)) degree[e.SrcVid]++;
            if (degree.ContainsKey(e.DstVid)) degree[e.DstVid]++;
        }

        // 无向连通分量 + 成环检测（边数 >= 节点数且连通则必有环，用并查集精确判定）
        var parent = nodes.ToDictionary(n => n.Id, n => n.Id, StringComparer.Ordinal);
        string Find(string x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
        var hasCycle = false;
        foreach (var e in edges)
        {
            if (!nodeIds.Contains(e.SrcVid) || !nodeIds.Contains(e.DstVid)) continue;
            if (e.SrcVid == e.DstVid) { hasCycle = true; continue; }
            var (ra, rb) = (Find(e.SrcVid), Find(e.DstVid));
            if (ra == rb) hasCycle = true;
            else parent[ra] = rb;
        }
        var components = nodeIds.Select(Find).Distinct().Count();

        var feedInCount = terminals.Count(t => t.Role == TerminalRole.FeedIn);
        var feedInEnergized = terminals.Count(t =>
            t.Role == TerminalRole.FeedIn && t.OperatingState == TerminalOperatingState.Energized);
        var tieCount = terminals.Count(t => t.Role == TerminalRole.Tie);

        var dto = new LineStructureDiagnosticsDto
        {
            NodeCount = nodes.Count,
            EdgeCount = edges.Count,
            NodeDegreeMax = degree.Count == 0 ? 0 : degree.Values.Max(),
            NodeDegreeOneCount = degree.Values.Count(d => d == 1),
            HasCycle = hasCycle,
            FeedInCount = feedInCount,
            FeedInEnergizedCount = feedInEnergized,
            TieCount = tieCount
        };

        if (feedInEnergized > 1)
            dto.Errors.Add($"同时投运的 FeedIn 端子最多 1 个（当前有 {feedInEnergized} 个投运）");
        if (nodes.Count > 1 && components > 1)
            dto.Warnings.Add($"结构不连通：{components} 个连通分量（只提示，不阻断）");
        if (hasCycle)
            dto.Warnings.Add("结构存在环（只提示，不阻断）");

        dto.Valid = dto.Errors.Count == 0;
        return dto;
    }
}

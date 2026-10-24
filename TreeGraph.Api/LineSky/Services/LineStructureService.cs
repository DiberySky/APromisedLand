using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.LineSky.Entities;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.LineSky.Dtos;
using TreeGraph.Shared.LineSky.Entities;
using TreeGraph.Shared.NodeEavSky.Dtos;
using TreeGraph.Shared.NodeSky.Entities;

namespace TreeGraph.Api.LineSky.Services;

/// <summary>
/// 线路结构服务 —— R7 边集薄层：
///   节点集 = nodes where line_id=lineId（host_type='LineNode'）；
///   边集 = relation_attribute_values 中两端均属本线的记录（一条记录 = 一条有向边）；
///   链序 = 沿 line_segment 链走（无入边者为起点；分叉/环按发现序）。
///
/// 拓扑模式校验整体删除；只保留：禁自环、同线校验、诊断（连通/成环/度数，只报告不阻断）。
/// 端子校验只留「投运 FeedIn ≤ 1」。段 Weight 已砍（DisplayName → relation.name）。
/// 写边经 EAV 写入链（校验/审计/RelationChangedEvent 复用 R6 管道），
/// 不再发布线路结构专用变更事件（R7 已删除）。
/// </summary>
public class LineStructureService
{
    /// <summary>seed 实体类型：线路结构。</summary>
    private const string StructureEntityType = "LineStructure";

    /// <summary>seed 链边属性：下一段（relation_type=line_segment）。</summary>
    private const string NextAttribute = "next";

    private readonly TreeGraphDbContext _db;
    private readonly EavWriteService _eavWrite;
    private readonly InodeOwnerGuardRegistry _ownerGuards;

    public LineStructureService(
        TreeGraphDbContext db,
        EavWriteService eavWrite,
        InodeOwnerGuardRegistry ownerGuards)
    {
        _db = db;
        _eavWrite = eavWrite;
        _ownerGuards = ownerGuards;
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

        var nodes = await _db.Nodes.AsNoTracking()
            .Where(n => n.HostType == "LineNode" && n.LineId == lineId)
            .OrderBy(n => n.Name)
            .ToListAsync(ct);
        var nodeIds = nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);

        // 边集 = 两端均属本线的 relation 值（含 line_segment 与用户自定义分支边）
        var edges = nodeIds.Count == 0
            ? new List<TreeGraph.Api.NodeEavSky.Entities.RelationAttributeValue>()
            : await _db.RelationAttributeValues.AsNoTracking()
                .Where(r => nodeIds.Contains(r.EntityId) && nodeIds.Contains(r.ToNodeId))
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
            .Where(e => e.RelationType == "line_segment")
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
                EdgeId = e.ValueId,
                FromNodeId = e.EntityId,
                FromNodeName = nameMap.GetValueOrDefault(e.EntityId, ""),
                ToNodeId = e.ToNodeId,
                ToNodeName = nameMap.GetValueOrDefault(e.ToNodeId, ""),
                RelationType = e.RelationType,
                DisplayName = e.Name
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

        var nextAttrId = await GetNextAttributeIdAsync(ct);
        var nodeIds = await LineNodeIdsAsync(lineId, ct);
        var hasEdges = nextAttrId is not null && nodeIds.Count > 0
            && await _db.RelationAttributeValues.AnyAsync(
                r => r.AttributeId == nextAttrId
                  && nodeIds.Contains(r.EntityId) && nodeIds.Contains(r.ToNodeId), ct);
        if (hasEdges)
            throw new InvalidOperationException("该线路已存在结构段边，请使用替换或逐段调整");

        return await ReplaceAsync(lineId, req, ct);
    }

    /// <summary>全量替换：段边集 + 端子集（同事务；段边经 EAV 写入链逐条落）。</summary>
    public async Task<LineStructureDto?> ReplaceAsync(
        string lineId, LineStructureRequest req, CancellationToken ct)
    {
        if (!await LineExistsAsync(lineId, ct)) return null;

        var nodeIds = await LineNodeIdsAsync(lineId, ct);
        var desired = ValidateSegments(req.Segments, nodeIds);

        // NpgsqlRetryingExecutionStrategy 不支持裸用户事务：经 CreateExecutionStrategy 包成可重试单元
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // 1. 段边全量替换（仅 line_segment；用户自定义分支边不动）
            var nextAttrId = await GetNextAttributeIdAsync(ct);
            if (nextAttrId is not null)
            {
                var existing = nodeIds.Count == 0
                    ? new List<TreeGraph.Api.NodeEavSky.Entities.RelationAttributeValue>()
                    : await _db.RelationAttributeValues
                        .Where(r => r.AttributeId == nextAttrId
                                 && nodeIds.Contains(r.EntityId) && nodeIds.Contains(r.ToNodeId))
                        .ToListAsync(ct);

                var desiredSet = desired
                    .Select(d => (d.FromNodeId, d.ToNodeId))
                    .ToHashSet();

                // 先删不需要的旧边（释放 from 的单值占用）
                foreach (var e in existing.Where(e => !desiredSet.Contains((e.EntityId, e.ToNodeId))))
                    await WriteNextAsync(e.EntityId, null, null, ct);

                // 再写新边（已存在且一致的自然跳过）
                var existingSet = existing
                    .Select(e => (e.EntityId, e.ToNodeId))
                    .ToHashSet();
                foreach (var d in desired.Where(d => !existingSet.Contains((d.FromNodeId, d.ToNodeId))))
                    await WriteNextAsync(d.FromNodeId, d.ToNodeId, d.DisplayName, ct);
            }
            else if (desired.Count > 0)
            {
                throw new InvalidOperationException("链边属性种子（LineStructure.next）缺失，无法写段边");
            }

            // 2. 端子全量替换（null = 清空；PUT = 全量替换语义，与「清空结构」一致）
            await ReplaceTerminalsAsync(lineId, nodeIds, req.Terminals ?? [], ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return await GetAsync(lineId, ct);
        });
    }

    // ============================================================
    // 单段加/删
    // ============================================================

    /// <summary>新增一段（写 seed 属性 next 的 line_segment 边；节点须同属该线）。</summary>
    public async Task<LineStructureDto> AddSegmentAsync(
        string lineId, AddLineSegmentRequest req, CancellationToken ct)
    {
        if (!await LineExistsAsync(lineId, ct))
            throw new KeyNotFoundException($"线路不存在: {lineId}");

        var nodeIds = await LineNodeIdsAsync(lineId, ct);
        ValidateSegmentEndpoints(req.FromNodeId, req.ToNodeId, nodeIds);

        await WriteNextAsync(req.FromNodeId, req.ToNodeId, req.DisplayName, ct);
        return (await GetAsync(lineId, ct))!;
    }

    /// <summary>删除一段（按边实例 value_id；两端均属本线才可删）。</summary>
    public async Task<LineStructureDto> DeleteSegmentAsync(
        string lineId, string edgeId, CancellationToken ct)
    {
        var edge = await _db.RelationAttributeValues
            .FirstOrDefaultAsync(r => r.ValueId == edgeId, ct);
        if (edge is null)
            throw new KeyNotFoundException($"段边不存在: {edgeId}");

        var nodeIds = await LineNodeIdsAsync(lineId, ct);
        if (!nodeIds.Contains(edge.EntityId) || !nodeIds.Contains(edge.ToNodeId))
            throw new ArgumentException($"段边 {edgeId} 不属于线路 {lineId}");

        // 经 EAV 写入链删边（审计 + RelationChangedEvent 复用 R6 管道）
        var attrName = await _db.AttributeCatalog
            .Where(a => a.AttributeId == edge.AttributeId)
            .Select(a => a.AttributeName)
            .FirstOrDefaultAsync(ct);
        if (attrName is null)
            throw new InvalidOperationException($"段边属性定义缺失: {edge.AttributeId}");

        try
        {
            await _eavWrite.PatchAsync(
                edge.EntityId, edge.EntityType,
                new Dictionary<string, object?> { [attrName] = null },
                changedBy: "system", correlationId: null, ct);
        }
        catch (EavConflictException ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }

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

    private async Task<HashSet<string>> LineNodeIdsAsync(string lineId, CancellationToken ct)
        => (await _db.Nodes.AsNoTracking()
                .Where(n => n.HostType == "LineNode" && n.LineId == lineId)
                .Select(n => n.Id)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

    private async Task<string?> GetNextAttributeIdAsync(CancellationToken ct)
        => await _db.AttributeCatalog
            .Where(a => a.EntityType == StructureEntityType
                     && a.AttributeName == NextAttribute && !a.IsDeleted)
            .Select(a => a.AttributeId)
            .FirstOrDefaultAsync(ct);

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
                    $"节点 {s.FromNodeId} 有多条出边：next 为单值属性，分支请定义新 relation 属性");
            result.Add(s);
        }
        return result;
    }

    /// <summary>经 EAV 写入链写/删 next 边（toId 为 null = 删边）。</summary>
    private async Task WriteNextAsync(
        string fromId, string? toId, string? displayName, CancellationToken ct)
    {
        try
        {
            await _eavWrite.PatchAsync(
                fromId, StructureEntityType,
                new Dictionary<string, object?>
                {
                    [NextAttribute] = toId is null
                        ? null
                        : new EavRelationValue { ToNodeId = toId, Name = displayName }
                },
                changedBy: "system", correlationId: null, ct);
        }
        catch (EavConflictException ex)
        {
            // Line 家族：InvalidOperationException 映射 409
            throw new InvalidOperationException(ex.Message, ex);
        }
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
        List<TreeGraph.Api.NodeEavSky.Entities.RelationAttributeValue> segmentEdges)
    {
        if (nodes.Count == 0) return new List<string>();

        var outgoing = segmentEdges
            .GroupBy(e => e.EntityId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ToNodeId).ToList(), StringComparer.Ordinal);
        var hasIncoming = segmentEdges.Select(e => e.ToNodeId).ToHashSet(StringComparer.Ordinal);

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
        List<TreeGraph.Api.NodeEavSky.Entities.RelationAttributeValue> edges,
        List<LineTerminal> terminals)
    {
        var nodeIds = nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var degree = nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
        foreach (var e in edges)
        {
            if (degree.ContainsKey(e.EntityId)) degree[e.EntityId]++;
            if (degree.ContainsKey(e.ToNodeId)) degree[e.ToNodeId]++;
        }

        // 无向连通分量 + 成环检测（边数 >= 节点数且连通则必有环，用并查集精确判定）
        var parent = nodes.ToDictionary(n => n.Id, n => n.Id, StringComparer.Ordinal);
        string Find(string x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
        var hasCycle = false;
        foreach (var e in edges)
        {
            if (!nodeIds.Contains(e.EntityId) || !nodeIds.Contains(e.ToNodeId)) continue;
            if (e.EntityId == e.ToNodeId) { hasCycle = true; continue; }
            var (ra, rb) = (Find(e.EntityId), Find(e.ToNodeId));
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

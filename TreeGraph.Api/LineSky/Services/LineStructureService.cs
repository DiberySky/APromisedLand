using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.EdgeSky.Services;
using TreeGraph.Api.LineSky.Entities;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.LineSky.Dtos;
using TreeGraph.Shared.LineSky.Entities;
using TreeGraph.Shared.Messaging;

namespace TreeGraph.Api.LineSky.Services;

/// <summary>
/// 线路结构服务：头表 + 段 + 外部连接端子。
///
/// 设计要点：
///   - 空结构 = 无头表：删掉最后一段时同事务删除头表；PUT 传空结构等价于清空。
///   - 段顺序不落库，按拓扑推导（Linear 为链、Radial/Network 为遍历序）。
///   - 结构校验与 EdgeInstanceService 的全局图校验职责分离：这里只做结构校验。
///   - 结构边只经本服务创建/删除（EdgeInstanceService 的 internal 绕过方法）。
/// </summary>
public class LineStructureService
{
    private readonly TreeGraphDbContext _db;
    private readonly EdgeInstanceService _edgeInst;
    private readonly InodeOwnerGuardRegistry _inodeGuards;
    private readonly IEventPublisher _publisher;

    public LineStructureService(
        TreeGraphDbContext db,
        EdgeInstanceService edgeInst,
        InodeOwnerGuardRegistry inodeGuards,
        IEventPublisher publisher)
    {
        _db = db;
        _edgeInst = edgeInst;
        _inodeGuards = inodeGuards;
        _publisher = publisher;
    }

    // ============================================================
    // 读取
    // ============================================================

    /// <summary>读取线路结构；线路不存在返回 null，无结构返回 Exists=false + 候选节点。</summary>
    public async Task<LineStructureDto?> GetAsync(string lineId, CancellationToken ct)
    {
        var lineName = await _db.Lines.AsNoTracking()
            .Where(l => l.Id == lineId)
            .Select(l => l.Name)
            .FirstOrDefaultAsync(ct);
        if (lineName is null) return null;

        var header = await _db.LineStructures.AsNoTracking()
            .FirstOrDefaultAsync(s => s.LineId == lineId, ct);

        var lineNodes = await _db.LineNodes.AsNoTracking()
            .Where(n => n.LineId == lineId)
            .OrderBy(n => n.Name)
            .Select(n => new { n.Id, n.Name })
            .ToListAsync(ct);
        var nameMap = lineNodes.ToDictionary(n => n.Id, n => n.Name, StringComparer.Ordinal);

        var segments = header is null
            ? new List<LineStructureSegment>()
            : await _db.LineStructureSegments.AsNoTracking()
                .Where(s => s.LineId == lineId)
                .ToListAsync(ct);

        if (header is null || segments.Count == 0)
        {
            return new LineStructureDto
            {
                Exists = false,
                LineId = lineId,
                LineName = lineName,
                TopologyMode = TopologyMode.Network,
                CandidateNodes = lineNodes
                    .Select(n => new LineStructureNodeDto { NodeId = n.Id, Name = n.Name })
                    .ToList(),
                Diagnostics = new LineStructureDiagnosticsDto()
            };
        }

        var def = await _db.EdgeDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(d => d.EdgeTypeId == header.EdgeTypeId, ct);

        var edgeIds = segments.Select(s => s.EdgeId).ToList();
        var instances = await _db.EdgeInstances.AsNoTracking()
            .Where(i => edgeIds.Contains(i.EdgeId))
            .Select(i => new { i.EdgeId, i.Weight, i.DisplayName })
            .ToListAsync(ct);
        var instanceMap = instances.ToDictionary(i => i.EdgeId, i => i, StringComparer.Ordinal);

        var terminals = await _db.LineTerminals.AsNoTracking()
            .Where(t => t.LineId == lineId)
            .ToListAsync(ct);

        var externalNames = await _inodeGuards.ResolveNodeNamesAsync(
            terminals.Where(t => t.ExternalNodeId is not null).Select(t => t.ExternalNodeId!), ct);

        var segmentDtos = segments
            .Select(s =>
            {
                instanceMap.TryGetValue(s.EdgeId, out var inst);
                return new LineStructureSegmentDto
                {
                    EdgeId = s.EdgeId,
                    FromNodeId = s.FromNodeId,
                    FromNodeName = nameMap.GetValueOrDefault(s.FromNodeId, s.FromNodeId),
                    ToNodeId = s.ToNodeId,
                    ToNodeName = nameMap.GetValueOrDefault(s.ToNodeId, s.ToNodeId),
                    Weight = inst?.Weight,
                    DisplayName = inst?.DisplayName
                };
            })
            .OrderBy(s => s.EdgeId, StringComparer.Ordinal)
            .ToList();

        var termMap = terminals.ToDictionary(t => t.NodeId, StringComparer.Ordinal);
        var nodeIds = segments
            .SelectMany(s => new[] { s.FromNodeId, s.ToNodeId })
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var validation = Validate(header.TopologyMode, nodeIds, segments, terminals);
        var ordered = DeriveOrder(header.TopologyMode, nodeIds, segments, terminals);

        // P1-3: 游离 LineNode Warning —— 线路下存在但未纳入任何段、也未设置端子的节点
        var allStructureNodeIds = nodeIds
            .Union(terminals.Select(t => t.NodeId), StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var orphanLineNodes = lineNodes
            .Where(n => !allStructureNodeIds.Contains(n.Id))
            .ToList();
        var warnings = new List<string>(validation.Warnings);
        if (orphanLineNodes.Count > 0)
            warnings.Add($"线路下有 {orphanLineNodes.Count} 个节点未纳入结构段（游离）：" +
                         string.Join("、", orphanLineNodes.Select(n => n.Name)));

        var nodeDtos = ordered.Select(id =>
        {
            termMap.TryGetValue(id, out var term);
            return new LineStructureNodeDto
            {
                NodeId = id,
                Name = nameMap.GetValueOrDefault(id, id),
                TerminalRole = term?.Role,
                OperatingState = term?.OperatingState,
                ExternalNodeId = term?.ExternalNodeId,
                ExternalNodeName = term?.ExternalNodeId is not null
                    ? externalNames.GetValueOrDefault(term.ExternalNodeId)
                    : null
            };
        }).ToList();

        var terminalDtos = terminals
            .Where(t => nodeIds.Contains(t.NodeId, StringComparer.Ordinal))
            .Select(t => new LineStructureNodeDto
            {
                NodeId = t.NodeId,
                Name = nameMap.GetValueOrDefault(t.NodeId, t.NodeId),
                TerminalRole = t.Role,
                OperatingState = t.OperatingState,
                ExternalNodeId = t.ExternalNodeId,
                ExternalNodeName = t.ExternalNodeId is not null
                    ? externalNames.GetValueOrDefault(t.ExternalNodeId)
                    : null
            })
            .ToList();

        return new LineStructureDto
        {
            Exists = true,
            LineId = lineId,
            LineName = lineName,
            EdgeTypeId = header.EdgeTypeId,
            EdgeTypeName = def?.TypeName,
            EdgeTypeDisplayName = def?.DisplayName,
            TopologyMode = header.TopologyMode,
            UpdatedAt = header.UpdatedAt,
            OrderedNodeIds = ordered,
            Nodes = nodeDtos,
            Segments = segmentDtos,
            Terminals = terminalDtos,
            CandidateNodes = lineNodes
                .Select(n => new LineStructureNodeDto { NodeId = n.Id, Name = n.Name })
                .ToList(),
            Diagnostics = new LineStructureDiagnosticsDto
            {
                Valid = validation.Errors.Count == 0,
                Errors = validation.Errors,
                Warnings = warnings,
                NodeCount = validation.NodeCount,
                EdgeCount = validation.EdgeCount,
                NodeDegreeMax = validation.MaxDegree,
                NodeDegreeOneCount = validation.DegreeOneCount,
                HasCycle = validation.HasCycle,
                FeedInCount = validation.FeedInCount,
                FeedInEnergizedCount = validation.FeedInEnergizedCount,
                TieCount = validation.TieCount
            }
        };
    }

    // ============================================================
    // 创建 / 全量替换
    // ============================================================

    /// <summary>创建线路结构（已有结构 → 409）。</summary>
    public async Task<LineStructureDto?> CreateAsync(
        string lineId, LineStructureRequest req, CancellationToken ct)
    {
        await EnsureLineExistsAsync(lineId, ct);

        var existed = await _db.LineStructures.AnyAsync(s => s.LineId == lineId, ct);
        if (existed)
            throw new InvalidOperationException("该线路已存在结构，请使用 PUT 全量替换；如需增量调整请使用段/端子接口");

        var plan = await BuildPlanAsync(lineId, req, ct);

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                await ApplyPlanAsync(lineId, plan, ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        await PublishLineChangedAsync(lineId, ct);
        return await GetAsync(lineId, ct);
    }

    /// <summary>
    /// 全量替换：先删旧结构（含其边实例）再按新请求重建。
    /// 请求无段/无有序节点时等价于「清空结构」。
    /// </summary>
    public async Task<LineStructureDto?> ReplaceAsync(
        string lineId, LineStructureRequest req, CancellationToken ct)
    {
        await EnsureLineExistsAsync(lineId, ct);

        var hasStructure = await _db.LineStructures.AnyAsync(s => s.LineId == lineId, ct);
        var emptyRequest = (req.Segments is null || req.Segments.Count == 0)
                           && (req.OrderedNodeIds is null || req.OrderedNodeIds.Count == 0);

        if (emptyRequest)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                try
                {
                    if (hasStructure) await RemoveStructureAsync(lineId, ct);
                    await tx.CommitAsync(ct);
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            });

            await PublishLineChangedAsync(lineId, ct);
            return await GetAsync(lineId, ct);
        }

        var plan = await BuildPlanAsync(lineId, req, ct);

        var strategy2 = _db.Database.CreateExecutionStrategy();
        await strategy2.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                if (hasStructure) await RemoveStructureAsync(lineId, ct);
                await ApplyPlanAsync(lineId, plan, ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        await PublishLineChangedAsync(lineId, ct);
        return await GetAsync(lineId, ct);
    }

    // ============================================================
    // 段增量
    // ============================================================

    /// <summary>新增一段（同线两端；模式校验通过才落库）。</summary>
    public async Task<LineStructureDto> AddSegmentAsync(
        string lineId, AddLineSegmentRequest req, CancellationToken ct)
    {
        var header = await GetHeaderOrThrowAsync(lineId, ct);
        if (string.IsNullOrWhiteSpace(req.FromNodeId) || string.IsNullOrWhiteSpace(req.ToNodeId))
            throw new ArgumentException("FromNodeId / ToNodeId 必填");
        if (req.FromNodeId == req.ToNodeId)
            throw new ArgumentException("线路结构不允许自环（from 与 to 相同）");

        await EnsureNodesBelongAsync(lineId, [req.FromNodeId, req.ToNodeId], ct);

        var segments = await _db.LineStructureSegments
            .Where(s => s.LineId == lineId)
            .ToListAsync(ct);
        var terminals = await _db.LineTerminals
            .Where(t => t.LineId == lineId)
            .ToListAsync(ct);

        // 重复段（同类型同方向不重复；无向时反向亦视为重复）
        var def = await _db.EdgeDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(d => d.EdgeTypeId == header.EdgeTypeId, ct)
            ?? throw new InvalidOperationException("结构引用的边类型不存在");
        var duplicated = segments.Any(s =>
            s.FromNodeId == req.FromNodeId && s.ToNodeId == req.ToNodeId
            || (!def.IsDirected
                && s.FromNodeId == req.ToNodeId && s.ToNodeId == req.FromNodeId));
        if (duplicated)
            throw new InvalidOperationException("该边已存在（同类型同方向不重复）");

        var prospective = segments
            .Select(s => new SegmentRow(s.EdgeId, s.FromNodeId, s.ToNodeId))
            .Append(new SegmentRow("(new)", req.FromNodeId, req.ToNodeId))
            .ToList();
        EnsureValid(header.TopologyMode, prospective, terminals);

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                await CreateSegmentRowAsync(
                    lineId, header.EdgeTypeId, req.FromNodeId, req.ToNodeId, req.Weight, req.DisplayName, ct);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        await PublishLineChangedAsync(lineId, ct);
        return (await GetAsync(lineId, ct))!;
    }

    /// <summary>删除一段；删掉最后一段时同事务删除结构头与端子。</summary>
    public async Task<LineStructureDto> DeleteSegmentAsync(
        string lineId, string edgeId, CancellationToken ct)
    {
        var header = await GetHeaderOrThrowAsync(lineId, ct);

        var segment = await _db.LineStructureSegments
            .FirstOrDefaultAsync(s => s.LineId == lineId && s.EdgeId == edgeId, ct)
            ?? throw new KeyNotFoundException("该段不存在");

        var remaining = await _db.LineStructureSegments
            .Where(s => s.LineId == lineId && s.EdgeId != edgeId)
            .ToListAsync(ct);

        if (remaining.Count > 0)
        {
            var terminals = await _db.LineTerminals
                .Where(t => t.LineId == lineId)
                .ToListAsync(ct);
            EnsureValid(
                header.TopologyMode,
                remaining.Select(s => new SegmentRow(s.EdgeId, s.FromNodeId, s.ToNodeId)).ToList(),
                terminals);
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                _db.LineStructureSegments.Remove(segment);
                await _db.SaveChangesAsync(ct);

                if (remaining.Count == 0)
                {
                    var terminals = await _db.LineTerminals
                        .Where(t => t.LineId == lineId)
                        .ToListAsync(ct);
                    _db.LineTerminals.RemoveRange(terminals);
                    _db.LineStructures.Remove(header);
                    await _db.SaveChangesAsync(ct);
                }

                await _edgeInst.DeleteInternalAsync(edgeId, ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        await PublishLineChangedAsync(lineId, ct);
        return (await GetAsync(lineId, ct))!;
    }

    // ============================================================
    // 端子
    // ============================================================

    /// <summary>新增/更新端子（按 nodeId 覆盖角色与外部连接）。</summary>
    public async Task<LineStructureDto> UpsertTerminalAsync(
        string lineId, AddLineTerminalRequest req, CancellationToken ct)
    {
        var header = await GetHeaderOrThrowAsync(lineId, ct);

        if (string.IsNullOrWhiteSpace(req.NodeId))
            throw new ArgumentException("NodeId 必填");

        await EnsureNodesBelongAsync(lineId, [req.NodeId], ct);

        var segments = await _db.LineStructureSegments
            .Where(s => s.LineId == lineId)
            .ToListAsync(ct);
        var structureNodeIds = segments
            .SelectMany(s => new[] { s.FromNodeId, s.ToNodeId })
            .ToHashSet(StringComparer.Ordinal);
        if (!structureNodeIds.Contains(req.NodeId))
            throw new ArgumentException("端子节点必须属于当前结构（先加入段再设置端子）");

        if (!string.IsNullOrWhiteSpace(req.ExternalNodeId))
            await _inodeGuards.EnsureNodeExistsAsync(req.ExternalNodeId, ct);

        var existing = await _db.LineTerminals
            .FirstOrDefaultAsync(t => t.LineId == lineId && t.NodeId == req.NodeId, ct);
        if (existing is null)
        {
            _db.LineTerminals.Add(new LineTerminal
            {
                LineId = lineId,
                NodeId = req.NodeId,
                Role = req.Role,
                OperatingState = req.OperatingState,
                ExternalNodeId = string.IsNullOrWhiteSpace(req.ExternalNodeId) ? null : req.ExternalNodeId
            });
        }
        else
        {
            existing.Role = req.Role;
            existing.OperatingState = req.OperatingState;
            existing.ExternalNodeId = string.IsNullOrWhiteSpace(req.ExternalNodeId) ? null : req.ExternalNodeId;
        }

        header.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await PublishNodeChangedAsync(req.NodeId, ct);
        await PublishLineChangedAsync(lineId, ct);
        return (await GetAsync(lineId, ct))!;
    }

    /// <summary>移除端子。</summary>
    public async Task<LineStructureDto> DeleteTerminalAsync(
        string lineId, string nodeId, CancellationToken ct)
    {
        var header = await GetHeaderOrThrowAsync(lineId, ct);

        var existing = await _db.LineTerminals
            .FirstOrDefaultAsync(t => t.LineId == lineId && t.NodeId == nodeId, ct)
            ?? throw new KeyNotFoundException("该端子不存在");

        _db.LineTerminals.Remove(existing);
        header.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await PublishNodeChangedAsync(nodeId, ct);
        await PublishLineChangedAsync(lineId, ct);
        return (await GetAsync(lineId, ct))!;
    }

    // ============================================================
    // 内部：计划构建 / 落库 / 清理
    // ============================================================

    private sealed record SegmentRow(string EdgeId, string FromNodeId, string ToNodeId);

    private sealed record StructurePlan(
        string EdgeTypeId,
        TopologyMode TopologyMode,
        List<(string From, string To, decimal? Weight, string? DisplayName)> Segments,
        List<(string NodeId, TerminalRole Role, TerminalOperatingState OperatingState, string? ExternalNodeId)> Terminals);

    private async Task<StructurePlan> BuildPlanAsync(
        string lineId, LineStructureRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.EdgeTypeId))
            throw new ArgumentException("EdgeTypeId 必填");

        var def = await _db.EdgeDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(d => d.EdgeTypeId == req.EdgeTypeId, ct)
            ?? throw new ArgumentException($"边类型不存在: {req.EdgeTypeId}");
        if (!def.IsLine)
            throw new ArgumentException($"边类型 {def.DisplayName} 不是线路结构类型（IsLine=false）");

        var segments = ResolveSegments(req);

        // 端点同线
        var nodeIds = segments
            .SelectMany(s => new[] { s.FromNodeId, s.ToNodeId })
            .Distinct(StringComparer.Ordinal)
            .ToList();
        await EnsureNodesBelongAsync(lineId, nodeIds, ct);

        // 端子输入：节点必须在结构内；外部连接存在性走宿主守卫
        var terminalInputs = req.Terminals ?? [];
        if (terminalInputs.Select(t => t.NodeId).Distinct(StringComparer.Ordinal).Count()
            != terminalInputs.Count)
            throw new ArgumentException("Terminals 存在重复节点");
        var structureNodeIds = nodeIds.ToHashSet(StringComparer.Ordinal);
        foreach (var t in terminalInputs)
        {
            if (!structureNodeIds.Contains(t.NodeId))
                throw new ArgumentException($"端子节点 {t.NodeId} 不在结构段端点内");
            if (!string.IsNullOrWhiteSpace(t.ExternalNodeId))
                await _inodeGuards.EnsureNodeExistsAsync(t.ExternalNodeId, ct);
        }

        // 重复段（同方向；无向边反向亦重复）
        var duplicate = segments
            .GroupBy(s => (s.FromNodeId, s.ToNodeId))
            .Any(g => g.Count() > 1)
            || (!def.IsDirected && segments
                .GroupBy(s => string.CompareOrdinal(s.FromNodeId, s.ToNodeId) <= 0
                    ? (s.FromNodeId, s.ToNodeId)
                    : (s.ToNodeId, s.FromNodeId))
                .Any(g => g.Count() > 1));
        if (duplicate)
            throw new InvalidOperationException("结构段存在重复边（同类型同方向不重复）");

        var segmentRows = segments
            .Select((s, i) => new SegmentRow($"new-{i}", s.FromNodeId, s.ToNodeId))
            .ToList();
        var terminalRows = terminalInputs
            .Select(t => new LineTerminal
            {
                NodeId = t.NodeId,
                Role = t.Role,
                OperatingState = t.OperatingState,
                ExternalNodeId = string.IsNullOrWhiteSpace(t.ExternalNodeId) ? null : t.ExternalNodeId
            })
            .ToList();
        EnsureValid(req.TopologyMode, segmentRows, terminalRows);

        return new StructurePlan(
            def.EdgeTypeId,
            req.TopologyMode,
            segments.Select(s => (s.FromNodeId, s.ToNodeId, s.Weight, (string?)null)).ToList(),
            terminalInputs.Select(t => (
                t.NodeId,
                t.Role,
                t.OperatingState,
                string.IsNullOrWhiteSpace(t.ExternalNodeId) ? null : t.ExternalNodeId)).ToList());
    }

    private static List<LineStructureSegmentInput> ResolveSegments(LineStructureRequest req)
    {
        var hasSegments = req.Segments is { Count: > 0 };
        var hasOrdered = req.OrderedNodeIds is { Count: > 0 };

        if (hasSegments && hasOrdered)
            throw new ArgumentException("Segments 与 OrderedNodeIds 只能二选一");

        if (hasSegments) return req.Segments!;

        if (hasOrdered)
        {
            var ids = req.OrderedNodeIds!;
            if (ids.Count < 2)
                throw new ArgumentException("OrderedNodeIds 至少需要 2 个节点才能形成线路");
            if (ids.Count != ids.Distinct(StringComparer.Ordinal).Count())
                throw new ArgumentException("OrderedNodeIds 存在重复节点，无法形成线路");

            var list = new List<LineStructureSegmentInput>(ids.Count - 1);
            for (var i = 0; i < ids.Count - 1; i++)
                list.Add(new LineStructureSegmentInput
                {
                    FromNodeId = ids[i],
                    ToNodeId = ids[i + 1]
                });
            return list;
        }

        throw new ArgumentException("结构至少需要一段（Segments 或 OrderedNodeIds 至少提供一个）");
    }

    private async Task ApplyPlanAsync(string lineId, StructurePlan plan, CancellationToken ct)
    {
        _db.LineStructures.Add(new LineStructure
        {
            LineId = lineId,
            EdgeTypeId = plan.EdgeTypeId,
            TopologyMode = plan.TopologyMode,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        foreach (var (from, to, weight, displayName) in plan.Segments)
            await CreateSegmentRowAsync(lineId, plan.EdgeTypeId, from, to, weight, displayName, ct);

        foreach (var (nodeId, role, operatingState, externalId) in plan.Terminals)
            _db.LineTerminals.Add(new LineTerminal
            {
                LineId = lineId,
                NodeId = nodeId,
                Role = role,
                OperatingState = operatingState,
                ExternalNodeId = externalId
            });

        await _db.SaveChangesAsync(ct);
    }

    private async Task CreateSegmentRowAsync(
        string lineId, string edgeTypeId,
        string fromNodeId, string toNodeId, decimal? weight, string? displayName,
        CancellationToken ct)
    {
        var dto = await _edgeInst.CreateLineEdgeInternalAsync(
            edgeTypeId,
            new CreateEdgeRequest
            {
                FromNodeId = fromNodeId,
                ToNodeId = toNodeId,
                Weight = weight,
                DisplayName = displayName
            },
            ct)
            ?? throw new InvalidOperationException("结构边创建失败");

        _db.LineStructureSegments.Add(new LineStructureSegment
        {
            LineId = lineId,
            EdgeId = dto.EdgeId,
            FromNodeId = fromNodeId,
            ToNodeId = toNodeId
        });
    }

    /// <summary>删除结构头、段、端子，并逐个删除其边实例（同事务调用方负责）。</summary>
    private async Task RemoveStructureAsync(string lineId, CancellationToken ct)
    {
        var header = await _db.LineStructures
            .FirstOrDefaultAsync(s => s.LineId == lineId, ct);
        if (header is null) return;

        var segments = await _db.LineStructureSegments
            .Where(s => s.LineId == lineId)
            .ToListAsync(ct);
        var terminals = await _db.LineTerminals
            .Where(t => t.LineId == lineId)
            .ToListAsync(ct);

        _db.LineStructureSegments.RemoveRange(segments);
        _db.LineTerminals.RemoveRange(terminals);
        _db.LineStructures.Remove(header);
        await _db.SaveChangesAsync(ct);

        foreach (var edgeId in segments.Select(s => s.EdgeId))
            await _edgeInst.DeleteInternalAsync(edgeId, ct);
    }

    // ============================================================
    // 内部：校验
    // ============================================================

    private sealed record ValidationOutcome(
        List<string> Errors,
        List<string> Warnings,
        int NodeCount,
        int EdgeCount,
        int MaxDegree,
        int DegreeOneCount,
        bool HasCycle,
        int FeedInCount,
        int FeedInEnergizedCount,
        int TieCount);

    private static ValidationOutcome Validate(
        TopologyMode mode,
        IReadOnlyCollection<string> nodeIds,
        IReadOnlyCollection<LineStructureSegment> segments,
        IReadOnlyCollection<LineTerminal> terminals)
        => ValidateCore(
            mode, nodeIds,
            segments.Select(s => new SegmentRow(s.EdgeId, s.FromNodeId, s.ToNodeId)).ToList(),
            terminals.Select(t => (t.NodeId, t.Role, t.OperatingState)).ToList());

    private static void EnsureValid(
        TopologyMode mode,
        List<SegmentRow> segments,
        IReadOnlyCollection<LineTerminal> terminals)
    {
        var nodeIds = segments
            .SelectMany(s => new[] { s.FromNodeId, s.ToNodeId })
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var outcome = ValidateCore(
            mode, nodeIds, segments,
            terminals.Select(t => (t.NodeId, t.Role, t.OperatingState)).ToList());
        if (outcome.Errors.Count > 0)
            throw new InvalidOperationException(
                $"线路结构拓扑校验失败（{mode}）：{string.Join("；", outcome.Errors)}");
    }

    /// <summary>
    /// 三种拓扑模式的校验。结构成员口径：只取段端点（未入任何段的线路节点不参与校验）。
    /// 端子投运校验：FeedIn 角色中 OperatingState=Energized 的数量 ≤ 1（多电源同一时刻只投一路）。
    /// </summary>
    private static ValidationOutcome ValidateCore(
        TopologyMode mode,
        IReadOnlyCollection<string> nodeIds,
        List<SegmentRow> segments,
        List<(string NodeId, TerminalRole Role, TerminalOperatingState OperatingState)> terminals)
    {
        var errors = new List<string>();

        var nodeSet = nodeIds.ToHashSet(StringComparer.Ordinal);
        if (segments.Count == 0)
            errors.Add("结构至少需要一段");

        // 端点必须属于本线路（数据库复合外键兜底）
        foreach (var s in segments)
        {
            if (!nodeSet.Contains(s.FromNodeId))
                errors.Add($"段端点 {s.FromNodeId} 不属于本线路");
            if (!nodeSet.Contains(s.ToNodeId))
                errors.Add($"段端点 {s.ToNodeId} 不属于本线路");
            if (s.FromNodeId == s.ToNodeId)
                errors.Add($"段 {s.EdgeId} 为自环（from = to）");
        }

        // 无向度
        var degree = nodeSet.ToDictionary(n => n, _ => 0, StringComparer.Ordinal);
        var adjacency = nodeSet.ToDictionary(
            n => n, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var s in segments)
        {
            if (!degree.ContainsKey(s.FromNodeId) || !degree.ContainsKey(s.ToNodeId)) continue;
            degree[s.FromNodeId]++;
            degree[s.ToNodeId]++;
            adjacency[s.FromNodeId].Add(s.ToNodeId);
            adjacency[s.ToNodeId].Add(s.FromNodeId);
        }

        var maxDegree = degree.Count == 0 ? 0 : degree.Values.Max();
        var degreeOne = degree.Values.Count(d => d == 1);

        // 连通性（无向 BFS）
        var connected = nodeSet.Count == 0 || nodeSet.All(n => adjacency[n].Count > 0);
        if (connected && nodeSet.Count > 0)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(nodeSet.First());
            visited.Add(nodeSet.First());
            while (queue.Count > 0)
            {
                foreach (var nbr in adjacency[queue.Dequeue()])
                    if (visited.Add(nbr)) queue.Enqueue(nbr);
            }
            if (visited.Count != nodeSet.Count)
            {
                connected = false;
                var orphans = nodeSet.Where(n => !visited.Contains(n)).ToList();
                errors.Add($"结构不连通（游离节点：{string.Join(", ", orphans)}）");
            }
        }

        // 成环（无向 DFS）
        var hasCycle = false;
        if (connected && nodeSet.Count > 0)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            bool Dfs(string cur, string? parent)
            {
                visited.Add(cur);
                foreach (var nbr in adjacency[cur])
                {
                    if (nbr == parent) continue;
                    if (!visited.Add(nbr)) return true;
                    if (Dfs(nbr, cur)) return true;
                }
                return false;
            }
            hasCycle = Dfs(nodeSet.First(), null);
        }

        // 端子节点必须在结构内
        var feedInCount = 0;
        var feedInEnergizedCount = 0; // FeedIn 且 OperatingState=Energized 的数量
        var tieCount = 0;
        foreach (var (nodeId, role, state) in terminals)
        {
            if (!nodeSet.Contains(nodeId))
                errors.Add($"端子节点 {nodeId} 不在结构段端点内");
            if (role == TerminalRole.FeedIn)
            {
                feedInCount++;
                if (state == TerminalOperatingState.Energized)
                    feedInEnergizedCount++;
            }
            if (role == TerminalRole.Tie) tieCount++;
        }

        // 多电源校验：同一时刻投运的 FeedIn 最多 1 个（Network/Radial/Linear 都适用）
        if (feedInEnergizedCount > 1)
            errors.Add($"同时投运的 FeedIn 端子最多 1 个（当前有 {feedInEnergizedCount} 个投运）");

        switch (mode)
        {
            case TopologyMode.Linear:
                if (maxDegree > 2)
                    errors.Add($"Linear 模式每点度应 ≤ 2（当前最大度 = {maxDegree}）");
                if (segments.Count > 0 && degreeOne != 2)
                    errors.Add($"Linear 模式应恰有两个端点（当前度为 1 的节点数 = {degreeOne}）");
                if (hasCycle)
                    errors.Add("Linear 模式不允许成环");
                break;

            case TopologyMode.Radial:
                if (hasCycle)
                    errors.Add("Radial 模式不允许成环");
                if (segments.Count > 0 && segments.Count != nodeSet.Count - 1)
                    errors.Add(
                        $"Radial 模式应为树（边数 {segments.Count} 应 = 节点数 {nodeSet.Count} - 1）");
                if (segments.Count > 0 && feedInCount != 1)
                    errors.Add($"Radial 模式应恰有 1 个 FeedIn 端子（当前 = {feedInCount}）");
                break;

            case TopologyMode.Network:
                // 网络式允许环与多电源、也不要求端子：连通性已在上方统一校验。
                break;
        }

        return new ValidationOutcome(
            errors, new List<string>(),
            nodeSet.Count, segments.Count,
            maxDegree, degreeOne, hasCycle,
            feedInCount, feedInEnergizedCount, tieCount);
    }

    /// <summary>按拓扑推导节点顺序（Linear 为链序；Radial/Network 为 BFS 序）。</summary>
    private static List<string> DeriveOrder(
        TopologyMode mode,
        IReadOnlyCollection<string> nodeIds,
        IReadOnlyCollection<LineStructureSegment> segments,
        IReadOnlyCollection<LineTerminal> terminals)
    {
        if (nodeIds.Count == 0) return [];

        var adjacency = nodeIds.ToDictionary(
            n => n, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var s in segments)
        {
            if (!adjacency.ContainsKey(s.FromNodeId) || !adjacency.ContainsKey(s.ToNodeId)) continue;
            adjacency[s.FromNodeId].Add(s.ToNodeId);
            adjacency[s.ToNodeId].Add(s.FromNodeId);
        }

        var feedIn = terminals
            .Where(t => t.Role == TerminalRole.FeedIn)
            .Select(t => t.NodeId)
            .FirstOrDefault(id => adjacency.ContainsKey(id));

        // Tie（联络点）在没有 FeedIn 时也可作为起点（联络点也可能是电源入口）
        var tie = terminals
            .Where(t => t.Role == TerminalRole.Tie)
            .Select(t => t.NodeId)
            .FirstOrDefault(id => adjacency.ContainsKey(id));

        string start;
        if (mode == TopologyMode.Linear)
        {
            // 链序优先按存储方向：从「无入边」的端点出发（有向边时避免逆序）
            var degreeOne = nodeIds.Where(n => adjacency[n].Count == 1).ToList();
            var incoming = segments
                .Select(s => s.ToNodeId)
                .ToHashSet(StringComparer.Ordinal);
            start = feedIn
                ?? degreeOne.FirstOrDefault(n => !incoming.Contains(n))
                ?? degreeOne.FirstOrDefault()
                ?? nodeIds.First();
        }
        else
        {
            start = feedIn
                ?? tie
                ?? nodeIds.FirstOrDefault(n => adjacency[n].Count == 1)
                ?? nodeIds.First();
        }

        var ordered = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        if (mode == TopologyMode.Linear)
        {
            // 从端点沿链走到底
            var cur = start;
            while (cur is not null && visited.Add(cur))
            {
                ordered.Add(cur);
                cur = adjacency[cur].FirstOrDefault(n => !visited.Contains(n));
            }
            // 链不完整（异常结构）时补全剩余节点，保证前端仍能展示
            ordered.AddRange(nodeIds.Where(n => !visited.Contains(n)));
            return ordered;
        }

        // Radial / Network：BFS 发现序
        var queue = new Queue<string>();
        queue.Enqueue(start);
        visited.Add(start);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            ordered.Add(cur);
            foreach (var nbr in adjacency[cur])
                if (visited.Add(nbr)) queue.Enqueue(nbr);
        }
        ordered.AddRange(nodeIds.Where(n => !visited.Contains(n)));
        return ordered;
    }

    // ============================================================
    // 内部：辅助
    // ============================================================

    private async Task EnsureLineExistsAsync(string lineId, CancellationToken ct)
    {
        var exists = await _db.Lines.AnyAsync(l => l.Id == lineId, ct);
        if (!exists) throw new KeyNotFoundException($"线路不存在: {lineId}");
    }

    private async Task<LineStructure> GetHeaderOrThrowAsync(string lineId, CancellationToken ct)
    {
        await EnsureLineExistsAsync(lineId, ct);
        return await _db.LineStructures.FirstOrDefaultAsync(s => s.LineId == lineId, ct)
            ?? throw new InvalidOperationException("该线路尚无结构，请先创建线路结构");
    }

    private async Task EnsureNodesBelongAsync(
        string lineId, IEnumerable<string> nodeIds, CancellationToken ct)
    {
        var ids = nodeIds.Distinct(StringComparer.Ordinal).ToList();
        var found = await _db.LineNodes.AsNoTracking()
            .Where(n => n.LineId == lineId && ids.Contains(n.Id))
            .Select(n => n.Id)
            .ToListAsync(ct);

        var missing = ids.Where(id => !found.Contains(id, StringComparer.Ordinal)).ToList();
        if (missing.Count > 0)
            throw new ArgumentException(
                $"节点不存在或不属于该线路: {string.Join(", ", missing)}");
    }

    private Task PublishLineChangedAsync(string lineId, CancellationToken ct)
        => _publisher.Publish(new InodeChangedEvent
        {
            InodeId = lineId,
            Kind = InodeChangeKind.Saved,
            PathAffected = false
        }, ct);

    private Task PublishNodeChangedAsync(string nodeId, CancellationToken ct)
        => _publisher.Publish(new InodeChangedEvent
        {
            InodeId = nodeId,
            Kind = InodeChangeKind.Saved,
            PathAffected = false
        }, ct);
}

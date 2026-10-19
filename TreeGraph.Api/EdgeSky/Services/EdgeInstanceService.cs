using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.Messaging;

namespace TreeGraph.Api.EdgeSky.Services;

/// <summary>
/// Edge Instance CRUD + 图查询。
///
/// 属性值复用 EAV：entity_type = EdgeDefinition.TypeName, entity_id = EdgeId。
/// 创建时支持一次性写入 Properties。
/// </summary>
public class EdgeInstanceService
{
    private readonly TreeGraphDbContext _db;
    private readonly EavWriteService _eavWrite;
    private readonly EavReadService _eavRead;
    private readonly IEdgeNodeValidator _nodeValidator;
    private readonly InodeOwnerGuardRegistry _inodeGuards;
    private readonly IPublishEndpoint _publishEndpoint;

    public EdgeInstanceService(
        TreeGraphDbContext db, EavWriteService eavWrite, EavReadService eavRead,
        IEdgeNodeValidator nodeValidator, InodeOwnerGuardRegistry inodeGuards,
        IPublishEndpoint publishEndpoint)
    {
        _db = db;
        _eavWrite = eavWrite;
        _eavRead = eavRead;
        _nodeValidator = nodeValidator;
        _inodeGuards = inodeGuards;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<EdgeInstanceDto?> CreateAsync(
        string edgeTypeId, CreateEdgeRequest req, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);
        if (def is null) return null;

        if (string.IsNullOrWhiteSpace(req.FromNodeId)
            || string.IsNullOrWhiteSpace(req.ToNodeId))
            throw new ArgumentException("FromNodeId / ToNodeId 必填");

        // 节点存在性 + 声明类型校验（FromNodeType/ToNodeType 非空时生效）
        await _nodeValidator.EnsureNodeAsync(def.FromNodeType, req.FromNodeId, ct);
        await _nodeValidator.EnsureNodeAsync(def.ToNodeType, req.ToNodeId, ct);

        // 重复检查：有向仅看同向；无向（IsDirected=false）反向亦视为重复
        var duplicate = await _db.EdgeInstances
            .AnyAsync(i => i.EdgeTypeId == edgeTypeId
                && (def.IsDirected
                    ? i.FromNodeId == req.FromNodeId && i.ToNodeId == req.ToNodeId
                    : (i.FromNodeId == req.FromNodeId && i.ToNodeId == req.ToNodeId)
                      || (i.FromNodeId == req.ToNodeId && i.ToNodeId == req.FromNodeId)), ct);
        if (duplicate)
            throw new InvalidOperationException("该边已存在（同类型同方向不重复）");

        var enforceSingleIncoming = def.IsLine && def.IsLinear;

        // 严格线性线路：入度 ≤ 1 友好前置校验（数据库部分唯一索引 uq_ei_single_incoming 兜底）
        if (enforceSingleIncoming)
        {
            var indegree = await _db.EdgeInstances
                .CountAsync(i => i.EdgeTypeId == edgeTypeId
                    && i.ToNodeId == req.ToNodeId, ct);
            if (indegree > 0)
                throw new InvalidOperationException(
                    $"线路结构 {def.DisplayName} 是严格线性，节点 {req.ToNodeId} 入度已 = 1，不能再接");
        }

        // 线路结构：自环 / 成环拦截
        if (def.IsLine)
        {
            if (req.FromNodeId == req.ToNodeId)
                throw new InvalidOperationException("线路结构不允许自环（from 与 to 相同）");

            if (await WouldCreateCycleAsync(edgeTypeId, req.FromNodeId, req.ToNodeId, ct))
                throw new InvalidOperationException(
                    $"新增边 {req.FromNodeId} → {req.ToNodeId} 会形成环，线路结构应无环");
        }

        var instance = new EdgeInstance
        {
            EdgeTypeId = edgeTypeId,
            FromNodeId = req.FromNodeId,
            ToNodeId = req.ToNodeId,
            Weight = req.Weight,
            DisplayName = req.DisplayName,
            EnforceSingleIncoming = enforceSingleIncoming,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.EdgeInstances.Add(instance);
        await _db.SaveChangesAsync(ct);

        // 如有 Properties，写入 EAV
        if (req.Properties is { Count: > 0 })
        {
            var values = JsonToDict(req.Properties);
            await _eavWrite.SaveAsync(
                instance.EdgeId, def.TypeName,
                values, changedBy: "edge", ct: ct);
        }

        // 图同步：边创建 → 通知图数据库即时写入（消费端幂等）
        await PublishAsync(instance, def, EdgeChangeKind.Created, ct);

        return await BuildDtoAsync(instance, def, ct);
    }

    public async Task<EdgeInstanceDto?> GetByIdAsync(string edgeId, CancellationToken ct)
    {
        var instance = await _db.EdgeInstances
            .FirstOrDefaultAsync(i => i.EdgeId == edgeId, ct);
        if (instance is null) return null;

        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == instance.EdgeTypeId, ct);
        if (def is null) return null;

        return await BuildDtoAsync(instance, def, ct);
    }

    /// <summary>查询某类型的所有边。</summary>
    public async Task<IReadOnlyList<EdgeInstanceDto>> ListByTypeAsync(
        string edgeTypeId, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);
        if (def is null) return Array.Empty<EdgeInstanceDto>();

        var instances = await _db.EdgeInstances
            .AsNoTracking()
            .Where(i => i.EdgeTypeId == edgeTypeId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return await BuildDtosAsync(instances, def, ct);
    }

    /// <summary>图查询：查某节点的出边（from）。</summary>
    public async Task<IReadOnlyList<EdgeInstanceDto>> FindOutgoingAsync(
        string edgeTypeId, string nodeId, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);
        if (def is null) return Array.Empty<EdgeInstanceDto>();

        // 无向关系：方向不适用，出边 = 与该节点相连的所有边
        var instances = def.IsDirected
            ? await _db.EdgeInstances
                .AsNoTracking()
                .Where(i => i.EdgeTypeId == edgeTypeId && i.FromNodeId == nodeId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync(ct)
            : await _db.EdgeInstances
                .AsNoTracking()
                .Where(i => i.EdgeTypeId == edgeTypeId
                    && (i.FromNodeId == nodeId || i.ToNodeId == nodeId))
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync(ct);

        return await BuildDtosAsync(instances, def, ct);
    }

    /// <summary>图查询：查某节点的入边（to）。</summary>
    public async Task<IReadOnlyList<EdgeInstanceDto>> FindIncomingAsync(
        string edgeTypeId, string nodeId, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);
        if (def is null) return Array.Empty<EdgeInstanceDto>();

        // 无向关系：方向不适用，入边 = 与该节点相连的所有边
        var instances = def.IsDirected
            ? await _db.EdgeInstances
                .AsNoTracking()
                .Where(i => i.EdgeTypeId == edgeTypeId && i.ToNodeId == nodeId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync(ct)
            : await _db.EdgeInstances
                .AsNoTracking()
                .Where(i => i.EdgeTypeId == edgeTypeId
                    && (i.FromNodeId == nodeId || i.ToNodeId == nodeId))
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync(ct);

        return await BuildDtosAsync(instances, def, ct);
    }

    /// <summary>
    /// BFS 遍历。从 StartNodeId 出发，按 Direction 沿边扩展，最多 MaxDepth 层。
    /// 每层用单次 SQL 查询（WHERE node_id = ANY @ids），避免 N+1。
    /// 无向关系（IsDirected=false）忽略 Direction，恒定按双向展开。
    /// </summary>
    public async Task<EdgeTraverseResult> TraverseAsync(
        string edgeTypeId, EdgeTraverseRequest req, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);
        if (def is null || req.MaxDepth < 1)
            return new EdgeTraverseResult(Array.Empty<EdgeInstanceDto>(),
                new[] { req.StartNodeId });

        var visited = new HashSet<string> { req.StartNodeId };
        var visitedEdgeIds = new HashSet<string>();
        var resultEdges = new List<EdgeInstance>();

        var frontier = new List<string> { req.StartNodeId };
        var maxDepth = Math.Clamp(req.MaxDepth, 1, 10);

        // 无向关系（IsDirected=false）：方向语义不适用，统一按双向展开
        var direction = def.IsDirected ? req.Direction : "both";

        for (int depth = 0; depth < maxDepth && frontier.Count > 0; depth++)
        {
            // 批量查当前层的边（只读，不进变更跟踪）
            var edges = direction switch
            {
                "in" => await _db.EdgeInstances
                    .AsNoTracking()
                    .Where(i => i.EdgeTypeId == edgeTypeId
                        && frontier.Contains(i.ToNodeId))
                    .ToListAsync(ct),
                "both" => await _db.EdgeInstances
                    .AsNoTracking()
                    .Where(i => i.EdgeTypeId == edgeTypeId
                        && (frontier.Contains(i.FromNodeId)
                            || frontier.Contains(i.ToNodeId)))
                    .ToListAsync(ct),
                _ => await _db.EdgeInstances   // 默认 out
                    .AsNoTracking()
                    .Where(i => i.EdgeTypeId == edgeTypeId
                        && frontier.Contains(i.FromNodeId))
                    .ToListAsync(ct),
            };

            // 去重边（同一条边可能被 both 方向在不同层遍历到）
            var nextFrontier = new HashSet<string>();
            foreach (var edge in edges)
            {
                if (!visitedEdgeIds.Add(edge.EdgeId)) continue;
                resultEdges.Add(edge);

                // 下一层节点（未访问过的）
                var neighbor = direction == "in"
                    ? edge.FromNodeId
                    : direction == "out"
                        ? edge.ToNodeId
                        : visited.Contains(edge.FromNodeId)
                            ? edge.ToNodeId : edge.FromNodeId;

                if (visited.Add(neighbor))
                    nextFrontier.Add(neighbor);
            }

            frontier = nextFrontier.ToList();
        }

        // 构建 DTO：单次 LoadBatchAsync 批量装载全部边的 EAV 属性（消除逐边查询的 N+1）
        var dtos = await BuildDtosAsync(resultEdges, def, ct);

        return new EdgeTraverseResult(dtos, visited.ToList());
    }

    public async Task<EdgeInstanceDto?> UpdateAsync(
        string edgeId, UpdateEdgeRequest req, CancellationToken ct)
    {
        var instance = await _db.EdgeInstances
            .FirstOrDefaultAsync(i => i.EdgeId == edgeId, ct);
        if (instance is null) return null;

        if (req.Weight.HasValue) instance.Weight = req.Weight.Value;
        if (req.DisplayName is not null) instance.DisplayName = req.DisplayName;
        instance.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == instance.EdgeTypeId, ct);

        // 图同步：边更新 → 通知图数据库即时刷新（消费端幂等）
        await PublishAsync(instance, def, EdgeChangeKind.Updated, ct);

        return def is null ? null : await BuildDtoAsync(instance, def, ct);
    }

    public async Task<bool> DeleteAsync(string edgeId, CancellationToken ct)
    {
        var instance = await _db.EdgeInstances
            .FirstOrDefaultAsync(i => i.EdgeId == edgeId, ct);
        if (instance is null) return false;

        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == instance.EdgeTypeId, ct);

        // 先清空自身数据才允许删本项（不采用级联删除）：
        // 边实例有独立行，其 EAV 属性值存于外部表，删除前必须先清空。
        if (def is not null)
        {
            var hasValues = await _db.AttributeValues
                    .AnyAsync(v => v.EntityId == edgeId && v.EntityType == def.TypeName, ct)
                || await _db.LocationAttributeValues
                    .AnyAsync(v => v.EntityId == edgeId && v.EntityType == def.TypeName, ct);
            if (hasValues)
                throw new InvalidOperationException(
                    "该边实例仍有属性数据，无法删除。请先清空其全部属性值。");
        }

        _db.EdgeInstances.Remove(instance);
        await _db.SaveChangesAsync(ct);

        // 图同步：边删除 → 通知图数据库即时移除（消费端幂等）
        await PublishAsync(instance, def, EdgeChangeKind.Deleted, ct);

        return true;
    }

    // ============================================================
    // 拓扑校验（入度/出度/成环）—— 迭代 Kahn，无递归
    // ============================================================

    /// <summary>
    /// 校验线路结构拓扑：入度/出度/成环。
    /// 返回轻量结果（Valid + Errors），供 EdgeMetadataService 在变更 IsLine/IsLinear 时使用。
    /// 严格线性线路（IsLine &amp;&amp; IsLinear）下，入度 &gt; 1 与成环计入 Errors。
    /// </summary>
    public async Task<(bool Valid, List<string> Errors)> ValidateTopologyAsync(
        string edgeTypeId, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);

        var pairs = await _db.EdgeInstances
            .Where(e => e.EdgeTypeId == edgeTypeId)
            .Select(e => new { e.FromNodeId, e.ToNodeId })
            .ToListAsync(ct);

        var analysis = AnalyzeTopology(pairs.Select(e => (e.FromNodeId, e.ToNodeId)).ToList());
        var treatAsStrict = def is { IsLine: true, IsLinear: true };

        var errors = new List<string>();

        foreach (var (nodeId, deg) in analysis.Indeg.Where(kv => kv.Value > 1))
        {
            var msg = $"节点 {nodeId} 入度 = {deg}（线路结构入度应 ≤ 1）";
            if (treatAsStrict) errors.Add(msg);
        }

        foreach (var nodeId in analysis.Cyclic)
        {
            var msg = $"节点 {nodeId} 参与成环（线路结构应无环）";
            if (treatAsStrict) errors.Add(msg);
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// 完整拓扑分析（供 LineService 包装为 LineValidationResult）。
    /// </summary>
    public async Task<TopologyAnalysis> AnalyzeTopologyAsync(
        string edgeTypeId, CancellationToken ct)
    {
        var pairs = await _db.EdgeInstances
            .Where(e => e.EdgeTypeId == edgeTypeId)
            .Select(e => new { e.FromNodeId, e.ToNodeId })
            .ToListAsync(ct);

        return AnalyzeTopology(pairs.Select(e => (e.FromNodeId, e.ToNodeId)).ToList());
    }

    // ============================================================
    // Helpers
    // ============================================================

    /// <summary>发布边变更事件（NebulaGraph 图同步；at-least-once，消费端幂等）。</summary>
    private async Task PublishAsync(
        EdgeInstance instance, EdgeDefinition? def, EdgeChangeKind kind, CancellationToken ct)
    {
        await _publishEndpoint.Publish(new EdgeChangedEvent
        {
            EdgeId = instance.EdgeId,
            Kind = kind,
            EdgeTypeName = def?.TypeName,
            FromNodeId = instance.FromNodeId,
            ToNodeId = instance.ToNodeId
        }, ct);
    }

    // 与 API 层（Program.cs）保持一致：Web 默认命名策略 + 数值/组合值转换器，
    // 保证边属性读回的 JSON 形状与实体端点一致。
    private static readonly JsonSerializerOptions SJson =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new NumericValueJsonConverter(),
                new DynamicCompositeValueJsonConverter()
            }
        };

    /// <summary>
    /// 单条组装边 DTO。EdgeDefinition 由调用方传入，避免每条边重复查询。
    /// 批量场景请使用 BuildDtosAsync（单次 LoadBatchAsync，消除逐边查询的 N+1）。
    /// </summary>
    private async Task<EdgeInstanceDto> BuildDtoAsync(
        EdgeInstance inst, EdgeDefinition def, CancellationToken ct)
    {
        // 查 EAV 属性（如有）
        var dyn = await _eavRead.LoadAsync(inst.EdgeId, def.TypeName, ct);
        // 两端都是 Inode 宿主：经守卫注册表解析显示名（未认领回退空串，前端回退显示 Id）
        var names = await _inodeGuards.ResolveNodeNamesAsync(
            [inst.FromNodeId, inst.ToNodeId], ct);

        return new EdgeInstanceDto(
            inst.EdgeId, inst.EdgeTypeId,
            def.TypeName, def.DisplayName,
            inst.FromNodeId, names.GetValueOrDefault(inst.FromNodeId, ""),
            inst.ToNodeId, names.GetValueOrDefault(inst.ToNodeId, ""),
            inst.Weight,
            inst.DisplayName,
            inst.CreatedAt, inst.UpdatedAt,
            ToProps(dyn));
    }

    /// <summary>
    /// 批量组装边 DTO：单次 LoadBatchAsync 装载全部边的 EAV 属性（2 次 SQL），
    /// 消除逐边 LoadAsync 的 N+1。保持 instances 的输入顺序。
    /// </summary>
    private async Task<List<EdgeInstanceDto>> BuildDtosAsync(
        IReadOnlyList<EdgeInstance> instances, EdgeDefinition def, CancellationToken ct)
    {
        if (instances.Count == 0) return new List<EdgeInstanceDto>();

        var propMap = (await _eavRead.LoadBatchAsync(
                instances.Select(i => i.EdgeId), def.TypeName, ct))
            .ToDictionary(d => d.EntityId, ToProps);

        // 两端 Inode 显示名：整批一次解析（每守卫一次 SQL），避免逐边 N+1
        var names = await _inodeGuards.ResolveNodeNamesAsync(
            instances.SelectMany(i => new[] { i.FromNodeId, i.ToNodeId }), ct);

        var result = new List<EdgeInstanceDto>(instances.Count);
        foreach (var inst in instances)
        {
            result.Add(new EdgeInstanceDto(
                inst.EdgeId, inst.EdgeTypeId,
                def.TypeName, def.DisplayName,
                inst.FromNodeId, names.GetValueOrDefault(inst.FromNodeId, ""),
                inst.ToNodeId, names.GetValueOrDefault(inst.ToNodeId, ""),
                inst.Weight,
                inst.DisplayName,
                inst.CreatedAt, inst.UpdatedAt,
                propMap.GetValueOrDefault(inst.EdgeId)));
        }
        return result;
    }

    /// <summary>将 DynamicEntity 的属性序列化为 DTO 的 JsonElement 形状（与实体端点读回一致）。</summary>
    private static Dictionary<string, JsonElement>? ToProps(DynamicEntity dyn)
    {
        if (dyn.Properties.Count == 0) return null;

        var props = new Dictionary<string, JsonElement>();
        foreach (var (k, v) in dyn.Properties)
        {
            try { props[k] = JsonSerializer.SerializeToElement(v, SJson); }
            catch { props[k] = JsonSerializer.SerializeToElement(v?.ToString() ?? ""); }
        }
        return props;
    }

    /// <summary>
    /// 成环检测：新增边 from→to 是否会成环 —— 即现有图中是否存在 to ⇝ from 的可达路径。
    /// 用带 UNION（去重、天然防环）的递归 CTE，避免 depth 硬截断。
    /// </summary>
    private async Task<bool> WouldCreateCycleAsync(
        string edgeTypeId, string fromId, string toId, CancellationToken ct)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                WITH RECURSIVE reach AS (
                    SELECT to_node_id AS n FROM edge_instances
                     WHERE edge_type_id = @et AND from_node_id = @start
                    UNION
                    SELECT e.to_node_id FROM edge_instances e
                      JOIN reach r ON e.from_node_id = r.n
                     WHERE e.edge_type_id = @et
                )
                SELECT 1 FROM reach WHERE n = @target
            ) THEN 1 ELSE 0 END AS "Value"
            """;

        var rows = await _db.Database
            .SqlQueryRaw<int>(sql,
                new Npgsql.NpgsqlParameter("et", edgeTypeId),
                new Npgsql.NpgsqlParameter("start", toId),
                new Npgsql.NpgsqlParameter("target", fromId))
            .ToListAsync(ct);
        return rows.Count > 0 && rows[0] == 1;
    }

    private static Dictionary<string, object?> JsonToDict(Dictionary<string, JsonElement> json)
    {
        var dict = new Dictionary<string, object?>(json.Count);
        foreach (var (k, v) in json)
        {
            dict[k] = v.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.TryGetInt64(out var i) ? i : v.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => v.ToString()
            };
        }
        return dict;
    }

    /// <summary>
    /// 迭代 Kahn 拓扑排序：返回入度表、出度表、未能消解的节点（= 成环节点）。
    /// 无递归，避免长链导致的栈溢出；O(V+E)。
    /// 注：环下游的节点也可能留在 Cyclic 中（Kahn 的已知特性），仅用于诊断提示。
    /// </summary>
    public static TopologyAnalysis AnalyzeTopology(List<(string From, string To)> edges)
    {
        var indeg = new Dictionary<string, int>();
        var outdeg = new Dictionary<string, int>();
        var adj = new Dictionary<string, List<string>>();

        foreach (var (from, to) in edges)
        {
            if (!indeg.ContainsKey(from)) indeg[from] = 0;
            indeg[to] = indeg.GetValueOrDefault(to) + 1;
            outdeg[from] = outdeg.GetValueOrDefault(from) + 1;
            if (!outdeg.ContainsKey(to)) outdeg[to] = 0;

            if (!adj.TryGetValue(from, out var list)) adj[from] = list = new();
            list.Add(to);
        }

        var remaining = new Dictionary<string, int>(indeg);
        var queue = new Queue<string>(remaining.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var processed = new HashSet<string>();

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            processed.Add(node);

            foreach (var next in adj.GetValueOrDefault(node, new()))
            {
                if (!remaining.ContainsKey(next)) continue;
                remaining[next]--;
                if (remaining[next] == 0) queue.Enqueue(next);
            }
        }

        var cyclic = indeg.Keys.Where(n => !processed.Contains(n)).ToList();
        return new TopologyAnalysis(indeg, outdeg, cyclic, indeg.Count);
    }
}

/// <summary>拓扑分析结果（入度/出度/成环节点）。</summary>
public sealed record TopologyAnalysis(
    Dictionary<string, int> Indeg,
    Dictionary<string, int> Outdeg,
    List<string> Cyclic,
    int NodeCount);

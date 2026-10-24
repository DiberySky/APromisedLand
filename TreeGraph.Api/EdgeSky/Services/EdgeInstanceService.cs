using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.MessagingSky;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.Messaging;
using TreeGraph.Shared.NodeSky.Entities;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.EdgeSky.Services;

/// <summary>
/// 边实例 CRUD + 图查询（R4 简化版）。
///
/// 只实现两个 Node 之间的有向/无向关系：
///   - 端点是 nodes(id) 的真外键，存在性由数据库兜底；
///   - type_name 是裸列，业务属性一律走 EAV（entity_type = type_name, entity_id = edgeId）；
///   - 不认识线路、不做拓扑校验，线路拓扑完全归 LineSky。
/// </summary>
public class EdgeInstanceService
{
    private readonly TreeGraphDbContext _db;
    private readonly EavWriteService _eavWrite;
    private readonly EavReadService _eavRead;
    private readonly InodeOwnerGuardRegistry _inodeGuards;
    private readonly IEventPublisher _publishEndpoint;

    public EdgeInstanceService(
        TreeGraphDbContext db, EavWriteService eavWrite, EavReadService eavRead,
        InodeOwnerGuardRegistry inodeGuards, IEventPublisher publishEndpoint)
    {
        _db = db;
        _eavWrite = eavWrite;
        _eavRead = eavRead;
        _inodeGuards = inodeGuards;
        _publishEndpoint = publishEndpoint;
    }

    // ============================================================
    // 写
    // ============================================================

    public async Task<EdgeInstanceDto> CreateAsync(CreateEdgeRequest req, CancellationToken ct)
    {
        var typeName = req.TypeName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(typeName))
            throw Invalid("typeName", "关系类型名必填");

        if (string.IsNullOrWhiteSpace(req.FromNodeId))
            throw Invalid("fromNodeId", "起点节点必填");
        if (string.IsNullOrWhiteSpace(req.ToNodeId))
            throw Invalid("toNodeId", "终点节点必填");

        // 端点存在性：nodes 单表批量查询（自环时去重后只查一次）
        var endpointIds = new[] { req.FromNodeId, req.ToNodeId }.Distinct().ToList();
        var existing = await _db.Nodes
            .AsNoTracking()
            .Where(n => endpointIds.Contains(n.Id))
            .Select(n => n.Id)
            .ToListAsync(ct);
        var missing = endpointIds.Except(existing).ToList();
        if (missing.Count > 0)
            throw Invalid("nodeId", $"节点不存在：{string.Join(", ", missing)}");

        // 重复判定：同类型同向；无向边反向亦视为重复（唯一索引兜底同向）
        var isDirected = req.IsDirected;
        var duplicate = await _db.EdgeInstances.AnyAsync(i =>
            i.TypeName == typeName
            && (isDirected
                ? i.FromNodeId == req.FromNodeId && i.ToNodeId == req.ToNodeId
                : (i.FromNodeId == req.FromNodeId && i.ToNodeId == req.ToNodeId)
                  || (i.FromNodeId == req.ToNodeId && i.ToNodeId == req.FromNodeId)), ct);
        if (duplicate)
            throw new InvalidOperationException("该边已存在（同类型同方向不重复）");

        var now = DateTimeOffset.UtcNow;
        var instance = new EdgeInstance
        {
            // 先确定 Id，才能在同一 SaveChanges 内注册 nodes 身份行（共享主键 FK）
            EdgeId = Guid.NewGuid().ToString(),
            TypeName = typeName,
            FromNodeId = req.FromNodeId,
            ToNodeId = req.ToNodeId,
            IsDirected = isDirected,
            CreatedAt = now,
            UpdatedAt = now
        };

        // 同一 SaveChanges（隐式事务）：EF 按 FK 依赖先插 nodes，再插 edge_instances
        _db.Nodes.Add(new Node
        {
            Id = instance.EdgeId,
            HostType = "EdgeInstance",
            Name = null,   // 边的名字是派生物（from名 → to名 / EAV 属性）
            CreatedAt = now
        });
        _db.EdgeInstances.Add(instance);
        await _db.SaveChangesAsync(ct);

        if (req.Properties is { Count: > 0 })
        {
            await _eavWrite.SaveAsync(
                instance.EdgeId, instance.TypeName,
                JsonToDict(req.Properties), changedBy: "edge", ct: ct);
        }

        await PublishAsync(instance, EdgeChangeKind.Created, ct);

        return (await BuildDtosAsync([instance], ct))[0];
    }

    public async Task<EdgeInstanceDto?> UpdateAsync(
        string edgeId, UpdateEdgeRequest req, CancellationToken ct)
    {
        var instance = await _db.EdgeInstances
            .FirstOrDefaultAsync(i => i.EdgeId == edgeId, ct);
        if (instance is null) return null;

        if (req.IsDirected is { } directed && directed != instance.IsDirected)
        {
            if (!directed)
            {
                // 改为无向：反向已有同类型边时即为重复
                var reverse = await _db.EdgeInstances.AnyAsync(i =>
                    i.EdgeId != edgeId
                    && i.TypeName == instance.TypeName
                    && i.FromNodeId == instance.ToNodeId
                    && i.ToNodeId == instance.FromNodeId, ct);
                if (reverse)
                    throw new InvalidOperationException("该边已存在（反向同类型），不能改为无向");
            }

            instance.IsDirected = directed;
            instance.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            await PublishAsync(instance, EdgeChangeKind.Updated, ct);
        }

        return (await BuildDtosAsync([instance], ct))[0];
    }

    public async Task<bool> DeleteAsync(string edgeId, CancellationToken ct)
    {
        var instance = await _db.EdgeInstances
            .FirstOrDefaultAsync(i => i.EdgeId == edgeId, ct);
        if (instance is null) return false;

        // 先清空自身数据才允许删本项（不采用级联删除）：
        // 边实例的 EAV 属性值存于外部表，删除前必须先清空。
        var hasValues = await _db.AttributeValues
                .AnyAsync(v => v.EntityId == edgeId && v.EntityType == instance.TypeName, ct)
            || await _db.LocationAttributeValues
                .AnyAsync(v => v.EntityId == edgeId && v.EntityType == instance.TypeName, ct);
        if (hasValues)
            throw new InvalidOperationException(
                "该边实例仍有属性数据，无法删除。请先清空其全部属性值。");

        // 挂载实体守卫：边作为宿主（inode_entity）仍有实体数据时拒绝
        var hasEntities = await _db.InodeEntities
            .AnyAsync(m => m.InodeId == edgeId, ct);
        if (hasEntities)
            throw new InvalidOperationException(
                "该边实例仍有挂载的实体数据，无法删除。请先清空其全部实体数据。");

        // 边已注册进 nodes，被其他边作为起点/终点引用时拒绝（FK Restrict 兜底）
        var referencedByEdge = await _db.EdgeInstances.AnyAsync(
            i => i.EdgeId != edgeId && (i.FromNodeId == edgeId || i.ToNodeId == edgeId), ct);
        if (referencedByEdge)
            throw new InvalidOperationException(
                "该边仍被其他边引用（作为端点），无法删除。请先解除引用。");

        // 类型声明（inode_entitytypes）不含业务数据，随宿主一并清理，避免孤儿声明
        var declarations = await _db.InodeEntityTypes
            .Where(x => x.InodeId == edgeId)
            .ToListAsync(ct);
        _db.InodeEntityTypes.RemoveRange(declarations);

        // 共享主键 FK（EdgeId 即 nodes.id）下先标记依赖方（edge_instances）为 Deleted，
        // 再标记主方（nodes）；顺序反过来 EF 会尝试原地置空标识外键并抛「键属性不可修改」。
        _db.EdgeInstances.Remove(instance);

        var nodeRow = await _db.Nodes.FirstOrDefaultAsync(n => n.Id == edgeId, ct);
        if (nodeRow is not null)
            _db.Nodes.Remove(nodeRow);

        await _db.SaveChangesAsync(ct);

        await PublishAsync(instance, EdgeChangeKind.Deleted, ct);

        return true;
    }

    // ============================================================
    // 读
    // ============================================================

    public async Task<EdgeInstanceDto?> GetByIdAsync(string edgeId, CancellationToken ct)
    {
        var instance = await _db.EdgeInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.EdgeId == edgeId, ct);
        if (instance is null) return null;

        return (await BuildDtosAsync([instance], ct))[0];
    }

    /// <summary>
    /// 全局边查询（跨类型）。可选过滤：类型名、端点节点集合（From 或 To 命中）、
    /// 关键字（匹配类型名或端点 nodes.name）。
    /// </summary>
    public async Task<IReadOnlyList<EdgeInstanceDto>> ListAsync(
        string? typeName,
        IReadOnlyCollection<string>? nodeIds,
        string? search,
        CancellationToken ct)
    {
        var query = _db.EdgeInstances.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(typeName))
        {
            var name = typeName.Trim();
            query = query.Where(i => i.TypeName == name);
        }

        if (nodeIds is { Count: > 0 })
        {
            var ids = nodeIds.ToList();
            query = query.Where(i => ids.Contains(i.FromNodeId) || ids.Contains(i.ToNodeId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            var matchingIds = _db.Nodes
                .Where(n => n.Name != null && n.Name.Contains(keyword))
                .Select(n => n.Id);
            query = query.Where(i =>
                i.TypeName.Contains(keyword)
                || matchingIds.Contains(i.FromNodeId)
                || matchingIds.Contains(i.ToNodeId));
        }

        var instances = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return await BuildDtosAsync(instances, ct);
    }

    /// <summary>图查询：查某节点的出边。无向实例按双向计入。</summary>
    public async Task<IReadOnlyList<EdgeInstanceDto>> FindOutgoingAsync(
        string? typeName, string nodeId, CancellationToken ct)
    {
        var query = _db.EdgeInstances.AsNoTracking()
            .Where(i => i.FromNodeId == nodeId
                || (!i.IsDirected && i.ToNodeId == nodeId));

        if (!string.IsNullOrWhiteSpace(typeName))
        {
            var name = typeName.Trim();
            query = query.Where(i => i.TypeName == name);
        }

        var instances = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return await BuildDtosAsync(instances, ct);
    }

    /// <summary>图查询：查某节点的入边。无向实例按双向计入。</summary>
    public async Task<IReadOnlyList<EdgeInstanceDto>> FindIncomingAsync(
        string? typeName, string nodeId, CancellationToken ct)
    {
        var query = _db.EdgeInstances.AsNoTracking()
            .Where(i => i.ToNodeId == nodeId
                || (!i.IsDirected && i.FromNodeId == nodeId));

        if (!string.IsNullOrWhiteSpace(typeName))
        {
            var name = typeName.Trim();
            query = query.Where(i => i.TypeName == name);
        }

        var instances = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return await BuildDtosAsync(instances, ct);
    }

    /// <summary>
    /// BFS 遍历。从 StartNodeId 出发，按 Direction 沿边扩展，最多 MaxDepth 层。
    /// 每条边按自身 is_directed 展开：有向按 from→to，无向双向。
    /// </summary>
    public async Task<EdgeTraverseResult> TraverseAsync(
        string typeName, EdgeTraverseRequest req, CancellationToken ct)
    {
        var start = req.StartNodeId;
        if (string.IsNullOrWhiteSpace(typeName)
            || string.IsNullOrWhiteSpace(start)
            || req.MaxDepth < 1)
            return new EdgeTraverseResult(Array.Empty<EdgeInstanceDto>(), [start]);

        var direction = req.Direction is "in" or "both" ? req.Direction : "out";
        var maxDepth = Math.Clamp(req.MaxDepth, 1, 10);

        var visited = new HashSet<string> { start };
        var visitedEdgeIds = new HashSet<string>();
        var resultEdges = new List<EdgeInstance>();

        var frontier = new List<string> { start };

        for (var depth = 0; depth < maxDepth && frontier.Count > 0; depth++)
        {
            var edges = direction switch
            {
                "in" => await _db.EdgeInstances.AsNoTracking()
                    .Where(i => i.TypeName == typeName
                        && (frontier.Contains(i.ToNodeId)
                            || (!i.IsDirected && frontier.Contains(i.FromNodeId))))
                    .ToListAsync(ct),
                "both" => await _db.EdgeInstances.AsNoTracking()
                    .Where(i => i.TypeName == typeName
                        && (frontier.Contains(i.FromNodeId)
                            || frontier.Contains(i.ToNodeId)))
                    .ToListAsync(ct),
                _ => await _db.EdgeInstances.AsNoTracking()
                    .Where(i => i.TypeName == typeName
                        && (frontier.Contains(i.FromNodeId)
                            || (!i.IsDirected && frontier.Contains(i.ToNodeId))))
                    .ToListAsync(ct),
            };

            var nextFrontier = new HashSet<string>();
            foreach (var edge in edges)
            {
                if (!visitedEdgeIds.Add(edge.EdgeId)) continue;
                resultEdges.Add(edge);

                var fromVisited = visited.Contains(edge.FromNodeId);
                var toVisited = visited.Contains(edge.ToNodeId);

                var neighbor = direction switch
                {
                    "in" => toVisited ? edge.FromNodeId
                        : (!edge.IsDirected && fromVisited ? edge.ToNodeId : null),
                    "both" => fromVisited && !toVisited ? edge.ToNodeId
                        : toVisited && !fromVisited ? edge.FromNodeId
                        : null,
                    _ => fromVisited ? edge.ToNodeId
                        : (!edge.IsDirected && toVisited ? edge.FromNodeId : null),
                };

                if (neighbor is not null && visited.Add(neighbor))
                    nextFrontier.Add(neighbor);
            }

            frontier = nextFrontier.ToList();
        }

        return new EdgeTraverseResult(
            await BuildDtosAsync(resultEdges, ct),
            visited.ToList());
    }

    // ============================================================
    // Helpers
    // ============================================================

    private async Task PublishAsync(
        EdgeInstance instance, EdgeChangeKind kind, CancellationToken ct)
    {
        await _publishEndpoint.Publish(new EdgeChangedEvent
        {
            EdgeId = instance.EdgeId,
            Kind = kind,
            EdgeTypeName = instance.TypeName,
            FromNodeId = instance.FromNodeId,
            ToNodeId = instance.ToNodeId,
            IsDirected = instance.IsDirected
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
    /// 批量组装边 DTO：按类型分组，每组一次 EAV 批量装载 + 一次名称解析，消除 N+1。
    /// 保持 instances 的输入顺序。
    /// </summary>
    private async Task<List<EdgeInstanceDto>> BuildDtosAsync(
        IReadOnlyList<EdgeInstance> instances, CancellationToken ct)
    {
        if (instances.Count == 0) return [];

        // 两端的显示名：整批一次解析（nodes 注册表单表查询，避免逐边 N+1）
        var names = await _inodeGuards.ResolveNodeNamesAsync(
            instances.SelectMany(i => new[] { i.FromNodeId, i.ToNodeId }), ct);

        var propMap = new Dictionary<string, Dictionary<string, JsonElement>?>(StringComparer.Ordinal);
        foreach (var group in instances.GroupBy(i => i.TypeName))
        {
            var loaded = await _eavRead.LoadBatchAsync(
                group.Select(i => i.EdgeId), group.Key, ct);
            foreach (var dyn in loaded)
                propMap[dyn.EntityId] = ToProps(dyn);
        }

        return instances.Select(inst => new EdgeInstanceDto(
            inst.EdgeId,
            inst.TypeName,
            inst.FromNodeId, names.GetValueOrDefault(inst.FromNodeId, ""),
            inst.ToNodeId, names.GetValueOrDefault(inst.ToNodeId, ""),
            inst.IsDirected,
            inst.CreatedAt, inst.UpdatedAt,
            propMap.GetValueOrDefault(inst.EdgeId))).ToList();
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

    private static EavValidationException Invalid(string field, string message)
        => new([new ValidationError(field, message)]);
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>
/// Edge Definition CRUD。
///
/// 删除 Definition 时级联清理：
///   1. EF Cascade 自动删 edge_instances
///   2. 手动删 attribute_catalog / attribute_values / location_attribute_values 中
///      entity_type = TypeName 的所有记录
/// </summary>
public class EdgeMetadataService
{
    private readonly TreeGraphDbContext _db;

    public EdgeMetadataService(TreeGraphDbContext db) => _db = db;

    public async Task<EdgeTypeDto> CreateAsync(CreateEdgeTypeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TypeName))
            throw new ArgumentException("TypeName 必填");
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            throw new ArgumentException("DisplayName 必填");
        if (!IsPascalCase(req.TypeName))
            throw new ArgumentException("TypeName 必须是 PascalCase（如 Collaboration）");

        // 唯一性检查
        var exists = await _db.EdgeDefinitions
            .AnyAsync(d => d.TypeName == req.TypeName, ct);
        if (exists)
            throw new InvalidOperationException($"Edge TypeName 已存在: {req.TypeName}");

        var def = new EdgeDefinition
        {
            TypeName = req.TypeName,
            DisplayName = req.DisplayName,
            FromNodeType = req.FromNodeType,
            ToNodeType = req.ToNodeType,
            IsGlobal = req.IsGlobal,
            IsDirected = req.IsDirected,
            Description = req.Description,
            IsActive = true,
            IsChain = req.IsChain,
            IsLinear = req.IsLinear,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.EdgeDefinitions.Add(def);
        await _db.SaveChangesAsync(ct);
        return ToDto(def);
    }

    public async Task<EdgeTypeDto?> GetByIdAsync(string edgeTypeId, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);
        return def is null ? null : ToDto(def);
    }

    public async Task<IReadOnlyList<EdgeTypeDto>> ListAsync(CancellationToken ct)
    {
        return await _db.EdgeDefinitions
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => ToDto(d))
            .ToListAsync(ct);
    }

    public async Task<EdgeTypeDto?> UpdateAsync(
        string edgeTypeId, UpdateEdgeTypeRequest req, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);
        if (def is null) return null;

        if (req.DisplayName is not null) def.DisplayName = req.DisplayName;
        if (req.FromNodeType is not null) def.FromNodeType = req.FromNodeType;
        if (req.ToNodeType is not null) def.ToNodeType = req.ToNodeType;
        if (req.IsGlobal.HasValue) def.IsGlobal = req.IsGlobal.Value;
        if (req.IsDirected.HasValue) def.IsDirected = req.IsDirected.Value;
        if (req.Description is not null) def.Description = req.Description;
        if (req.IsChain.HasValue) def.IsChain = req.IsChain.Value;
        if (req.IsLinear.HasValue) def.IsLinear = req.IsLinear.Value;
        def.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(def);
    }

    /// <summary>
    /// 硬删 Edge Definition + 级联清理 EAV 属性/值。
    /// 返回删除统计。
    /// </summary>
    public async Task<EdgeDeleteResult> DeleteAsync(string edgeTypeId, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);
        if (def is null) return new EdgeDeleteResult(false, 0, 0, 0);

        var typeName = def.TypeName;

        // 1. 统计要删的实例数（EF Cascade 会自动删 edge_instances）
        var instanceCount = await _db.EdgeInstances
            .CountAsync(i => i.EdgeTypeId == edgeTypeId, ct);

        // 2. 删 EAV 值
        var avCount = await _db.AttributeValues
            .Where(v => v.EntityType == typeName)
            .ExecuteDeleteAsync(ct);

        var lavCount = await _db.LocationAttributeValues
            .Where(v => v.EntityType == typeName)
            .ExecuteDeleteAsync(ct);

        // 3. 删 EAV 属性定义
        var attrCount = await _db.AttributeCatalog
            .Where(a => a.EntityType == typeName)
            .ExecuteDeleteAsync(ct);

        // 4. 删 Definition（Cascade 自动删 EdgeInstances）
        _db.EdgeDefinitions.Remove(def);
        await _db.SaveChangesAsync(ct);

        return new EdgeDeleteResult(true, instanceCount, avCount, attrCount);
    }

    private static EdgeTypeDto ToDto(EdgeDefinition d) => new(
        d.EdgeTypeId, d.TypeName, d.DisplayName,
        d.FromNodeType, d.ToNodeType,
        d.IsGlobal, d.IsDirected,
        d.Description, d.IsActive,
        d.IsChain, d.IsLinear);

    private static bool IsPascalCase(string s)
        => char.IsUpper(s[0])
            && s.Skip(1).All(c => char.IsLetterOrDigit(c) || c == '_');
}

// ============================================================
// Edge Instance Service
// ============================================================

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

    public EdgeInstanceService(
        TreeGraphDbContext db, EavWriteService eavWrite, EavReadService eavRead)
    {
        _db = db;
        _eavWrite = eavWrite;
        _eavRead = eavRead;
    }

    public async Task<EdgeInstanceDto?> CreateAsync(
        string edgeTypeId, CreateEdgeRequest req, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);
        if (def is null) return null;

        // 节点类型校验（EdgeDefinition 指定了 FromNodeType/ToNodeType 时）
        // 这里只做基础非空检查，更细的类型存在性校验由调用方或前端完成
        if (string.IsNullOrWhiteSpace(req.FromNodeId)
            || string.IsNullOrWhiteSpace(req.ToNodeId))
            throw new ArgumentException("FromNodeId / ToNodeId 必填");

        // 同方向重复检查（UNIQUE 约束会兜底，这里提前给出友好错误）
        var duplicate = await _db.EdgeInstances
            .AnyAsync(i => i.EdgeTypeId == edgeTypeId
                && i.FromNodeId == req.FromNodeId
                && i.ToNodeId == req.ToNodeId, ct);
        if (duplicate)
            throw new InvalidOperationException("该边已存在（同类型同方向不重复）");

        // 链式结构：入度 ≤ 1 校验（IsLinear=true 强制；IsLinear=false 仍建议 ≤1 但允许出度多）
        if (def.IsChain && def.IsLinear)
        {
            var indegree = await _db.EdgeInstances
                .CountAsync(i => i.EdgeTypeId == edgeTypeId
                    && i.ToNodeId == req.ToNodeId, ct);
            if (indegree > 0)
                throw new InvalidOperationException(
                    $"链式结构 {def.DisplayName} 是严格线性，节点 {req.ToNodeId} 入度已 = 1，不能再接");
        }

        var instance = new EdgeInstance
        {
            EdgeTypeId = edgeTypeId,
            FromNodeId = req.FromNodeId,
            ToNodeId = req.ToNodeId,
            Weight = req.Weight,
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

        return await BuildDtoAsync(instance, def.TypeName, ct);
    }

    public async Task<EdgeInstanceDto?> GetByIdAsync(string edgeId, CancellationToken ct)
    {
        var instance = await _db.EdgeInstances
            .FirstOrDefaultAsync(i => i.EdgeId == edgeId, ct);
        if (instance is null) return null;

        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == instance.EdgeTypeId, ct);
        if (def is null) return null;

        return await BuildDtoAsync(instance, def.TypeName, ct);
    }

    /// <summary>查询某类型的所有边。</summary>
    public async Task<IReadOnlyList<EdgeInstanceDto>> ListByTypeAsync(
        string edgeTypeId, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);
        if (def is null) return Array.Empty<EdgeInstanceDto>();

        var instances = await _db.EdgeInstances
            .Where(i => i.EdgeTypeId == edgeTypeId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        var result = new List<EdgeInstanceDto>(instances.Count);
        foreach (var inst in instances)
        {
            result.Add(await BuildDtoAsync(inst, def.TypeName, ct));
        }
        return result;
    }

    /// <summary>图查询：查某节点的出边（from）。</summary>
    public async Task<IReadOnlyList<EdgeInstanceDto>> FindOutgoingAsync(
        string edgeTypeId, string nodeId, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);
        if (def is null) return Array.Empty<EdgeInstanceDto>();

        var instances = await _db.EdgeInstances
            .Where(i => i.EdgeTypeId == edgeTypeId && i.FromNodeId == nodeId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        var result = new List<EdgeInstanceDto>(instances.Count);
        foreach (var inst in instances)
            result.Add(await BuildDtoAsync(inst, def.TypeName, ct));
        return result;
    }

    /// <summary>图查询：查某节点的入边（to）。</summary>
    public async Task<IReadOnlyList<EdgeInstanceDto>> FindIncomingAsync(
        string edgeTypeId, string nodeId, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);
        if (def is null) return Array.Empty<EdgeInstanceDto>();

        var instances = await _db.EdgeInstances
            .Where(i => i.EdgeTypeId == edgeTypeId && i.ToNodeId == nodeId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        var result = new List<EdgeInstanceDto>(instances.Count);
        foreach (var inst in instances)
            result.Add(await BuildDtoAsync(inst, def.TypeName, ct));
        return result;
    }

    /// <summary>
    /// BFS 遍历。从 StartNodeId 出发，按 Direction 沿边扩展，最多 MaxDepth 层。
    /// 每层用单次 SQL 查询（WHERE node_id = ANY @ids），避免 N+1。
    /// </summary>
    public async Task<EdgeTraverseResult> TraverseAsync(
        string edgeTypeId, EdgeTraverseRequest req, CancellationToken ct)
    {
        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == edgeTypeId, ct);
        if (def is null || req.MaxDepth < 1)
            return new EdgeTraverseResult(Array.Empty<EdgeInstanceDto>(),
                new[] { req.StartNodeId });

        var visited = new HashSet<string> { req.StartNodeId };
        var visitedEdgeIds = new HashSet<string>();
        var resultEdges = new List<EdgeInstance>();

        var frontier = new List<string> { req.StartNodeId };
        var maxDepth = Math.Clamp(req.MaxDepth, 1, 10);

        for (int depth = 0; depth < maxDepth && frontier.Count > 0; depth++)
        {
            // 批量查当前层的边
            var edges = req.Direction switch
            {
                "in" => await _db.EdgeInstances
                    .Where(i => i.EdgeTypeId == edgeTypeId
                        && frontier.Contains(i.ToNodeId))
                    .ToListAsync(ct),
                "both" => await _db.EdgeInstances
                    .Where(i => i.EdgeTypeId == edgeTypeId
                        && (frontier.Contains(i.FromNodeId)
                            || frontier.Contains(i.ToNodeId)))
                    .ToListAsync(ct),
                _ => await _db.EdgeInstances   // 默认 out
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
                var neighbor = req.Direction == "in"
                    ? edge.FromNodeId
                    : req.Direction == "out"
                        ? edge.ToNodeId
                        : visited.Contains(edge.FromNodeId)
                            ? edge.ToNodeId : edge.FromNodeId;

                if (visited.Add(neighbor))
                    nextFrontier.Add(neighbor);
            }

            frontier = nextFrontier.ToList();
        }

        // 构建 DTO（单次批量 BuildDto 会导致 N+1，但实际深度有限，边数量通常可控）
        var dtos = new List<EdgeInstanceDto>(resultEdges.Count);
        foreach (var e in resultEdges)
            dtos.Add(await BuildDtoAsync(e, def.TypeName, ct));

        return new EdgeTraverseResult(dtos, visited.ToList());
    }

    public async Task<EdgeInstanceDto?> UpdateAsync(
        string edgeId, UpdateEdgeRequest req, CancellationToken ct)
    {
        var instance = await _db.EdgeInstances
            .FirstOrDefaultAsync(i => i.EdgeId == edgeId, ct);
        if (instance is null) return null;

        if (req.Weight.HasValue) instance.Weight = req.Weight.Value;
        instance.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == instance.EdgeTypeId, ct);
        return def is null ? null : await BuildDtoAsync(instance, def.TypeName, ct);
    }

    public async Task<bool> DeleteAsync(string edgeId, CancellationToken ct)
    {
        var instance = await _db.EdgeInstances
            .FirstOrDefaultAsync(i => i.EdgeId == edgeId, ct);
        if (instance is null) return false;

        var def = await _db.EdgeDefinitions
            .FirstOrDefaultAsync(d => d.EdgeTypeId == instance.EdgeTypeId, ct);

        // 删 EAV 属性值
        if (def is not null)
        {
            await _db.AttributeValues
                .Where(v => v.EntityId == edgeId && v.EntityType == def.TypeName)
                .ExecuteDeleteAsync(ct);
            await _db.LocationAttributeValues
                .Where(v => v.EntityId == edgeId && v.EntityType == def.TypeName)
                .ExecuteDeleteAsync(ct);
        }

        _db.EdgeInstances.Remove(instance);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ============================================================
    // Helpers
    // ============================================================

    private async Task<EdgeInstanceDto> BuildDtoAsync(
        EdgeInstance inst, string entityType, CancellationToken ct)
    {
        // 查 EdgeDefinition（填充类型名/显示名）
        var def = await _db.EdgeDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.EdgeTypeId == inst.EdgeTypeId, ct);

        // 查 EAV 属性（如有）
        Dictionary<string, JsonElement>? props = null;
        var dyn = await _eavRead.LoadAsync(inst.EdgeId, entityType, ct);
        if (dyn.Properties.Count > 0)
        {
            props = new Dictionary<string, JsonElement>();
            foreach (var (k, v) in dyn.Properties)
            {
                try { props[k] = JsonSerializer.SerializeToElement(v); }
                catch { props[k] = JsonSerializer.SerializeToElement(v?.ToString() ?? ""); }
            }
        }

        return new EdgeInstanceDto(
            inst.EdgeId, inst.EdgeTypeId,
            def?.TypeName ?? "",
            def?.DisplayName ?? "",
            inst.FromNodeId, "",
            inst.ToNodeId, "",
            inst.Weight,
            inst.CreatedAt, inst.UpdatedAt,
            props);
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
}

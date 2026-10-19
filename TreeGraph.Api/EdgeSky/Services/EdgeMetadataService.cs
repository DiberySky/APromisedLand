using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.EdgeSky.Dtos;

namespace TreeGraph.Api.EdgeSky.Services;

/// <summary>
/// Edge Definition CRUD。
///
/// 删除 Definition 不采用级联删除：边实例 / 活动属性非空时抛 InvalidOperationException 拒绝，
/// 需先清空子项与引用后才可删除。
/// </summary>
public class EdgeMetadataService
{
    private readonly TreeGraphDbContext _db;
    private readonly EdgeInstanceService _edgeInst;
    private readonly IAttributeCache _attrCache;

    public EdgeMetadataService(
        TreeGraphDbContext db, EdgeInstanceService edgeInst, IAttributeCache attrCache)
    {
        _db = db;
        _edgeInst = edgeInst;
        _attrCache = attrCache;
    }

    public async Task<EdgeTypeDto> CreateAsync(CreateEdgeTypeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TypeName))
            throw new ArgumentException("TypeName 必填");
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            throw new ArgumentException("DisplayName 必填");
        if (!IsPascalCase(req.TypeName))
            throw new ArgumentException("TypeName 必须是 PascalCase（如 Collaboration）");

        // IsGlobal=false 表示该关系仅限声明的节点类型；未声明节点类型则无约束，等同于全局。
        if (!req.IsGlobal
            && (string.IsNullOrWhiteSpace(req.FromNodeType)
                || string.IsNullOrWhiteSpace(req.ToNodeType)))
            throw new ArgumentException(
                "IsGlobal=false 时必须同时声明 FromNodeType 与 ToNodeType（否则该关系类型无约束，等同于全局）");

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
            IsLine = req.IsLine,
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

        // 有效值校验：IsGlobal=false 必须能定位到声明的节点类型（与 CreateAsync 同规则）
        var effIsGlobal = req.IsGlobal ?? def.IsGlobal;
        var effFromType = req.FromNodeType ?? def.FromNodeType;
        var effToType = req.ToNodeType ?? def.ToNodeType;
        if (!effIsGlobal
            && (string.IsNullOrWhiteSpace(effFromType) || string.IsNullOrWhiteSpace(effToType)))
            throw new ArgumentException(
                "IsGlobal=false 时必须同时声明 FromNodeType 与 ToNodeType（否则该关系类型无约束，等同于全局）");

        var newIsLine = req.IsLine ?? def.IsLine;
        var newIsLinear = req.IsLinear ?? def.IsLinear;

        // ★ 拓扑约束变更：先按新值做一次校验，若破坏现有拓扑则整体回滚拒绝。
        if (newIsLine != def.IsLine || newIsLinear != def.IsLinear)
        {
            var oldIsLine = def.IsLine;
            var oldIsLinear = def.IsLinear;
            def.IsLine = newIsLine;
            def.IsLinear = newIsLinear;

            // ValidateTopologyAsync 与当前 ApiResponse 请求共享同一 Scoped DbContext，
            // 会读取上面已修改（尚未保存）的跟踪实体值。
            var validation = await _edgeInst.ValidateTopologyAsync(def.EdgeTypeId, ct);
            if (!validation.Valid)
            {
                def.IsLine = oldIsLine;
                def.IsLinear = oldIsLinear;
                throw new InvalidOperationException(
                    $"变更线路/线性约束会破坏现有拓扑：{string.Join("；", validation.Errors)}");
            }

            // 同步存量实例的"严格线性"镜像标记
            var enforce = newIsLine && newIsLinear;
            await _db.EdgeInstances
                .Where(i => i.EdgeTypeId == def.EdgeTypeId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.EnforceSingleIncoming, enforce), ct);
        }

        if (req.DisplayName is not null) def.DisplayName = req.DisplayName;
        if (req.FromNodeType is not null) def.FromNodeType = req.FromNodeType;
        if (req.ToNodeType is not null) def.ToNodeType = req.ToNodeType;
        if (req.IsGlobal.HasValue) def.IsGlobal = req.IsGlobal.Value;
        if (req.IsDirected.HasValue) def.IsDirected = req.IsDirected.Value;
        if (req.Description is not null) def.Description = req.Description;
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

        // 子项为空方可删除本项：仍有边实例时拒绝
        var instanceCount = await _db.EdgeInstances
            .CountAsync(i => i.EdgeTypeId == edgeTypeId, ct);
        if (instanceCount > 0)
            throw new InvalidOperationException(
                $"该边类型仍有 {instanceCount} 条边实例，无法删除。请先删除全部边实例。");

        // 引用为空方可删除本项：仍有活动属性定义时拒绝
        var attrCount = await _db.AttributeCatalog
            .CountAsync(a => a.EntityType == typeName && !a.IsDeleted, ct);
        if (attrCount > 0)
            throw new InvalidOperationException(
                $"该边类型仍有 {attrCount} 个活动属性，无法删除。请先删除其属性。");

        _db.EdgeDefinitions.Remove(def);
        await _db.SaveChangesAsync(ct);

        // 该类型名可能仍被属性定义缓存引用，删除类型后一并失效，避免读到悬空元数据。
        _attrCache.Invalidate(typeName);

        return new EdgeDeleteResult(true, 0, 0, 0);
    }

    private static EdgeTypeDto ToDto(EdgeDefinition d) => new(
        d.EdgeTypeId, d.TypeName, d.DisplayName,
        d.FromNodeType, d.ToNodeType,
        d.IsGlobal, d.IsDirected,
        d.Description, d.IsActive,
        d.IsLine, d.IsLinear);

    private static bool IsPascalCase(string s)
        => char.IsUpper(s[0])
            && s.Skip(1).All(c => char.IsLetterOrDigit(c) || c == '_');
}

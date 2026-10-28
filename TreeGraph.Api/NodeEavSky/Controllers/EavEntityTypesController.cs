using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Controllers;

/// <summary>
/// 实体类型元数据端点。
///
/// 数据源已从 AttributeCatalog 聚合改为 EntityTypeCatalog 表。
/// AttributeCatalog 提供统计（属性数 / 可搜索数），
/// EntityTypeCatalog 提供类型本身（DisplayName / Description）。
/// </summary>
[ApiController]
[Route("api/eav/entity-types")]
public class EavEntityTypesController : ControllerBase
{
    private readonly TreeGraphDbContext _db;
    private readonly IAttributeCache _attrCache;

    public EavEntityTypesController(TreeGraphDbContext db, IAttributeCache attrCache)
    {
        _db = db;
        _attrCache = attrCache;
    }

    // ============================================================
    // 列表
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        // 从 EntityTypeCatalog 拿活动类型（纯全局共享，无空间归属）
        var query = _db.EntityTypes.Where(t => !t.IsDeleted);

        var types = await query
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.EntityType)
            .AsNoTracking()
            .ToListAsync(ct);

        // 从 AttributeCatalog 聚合属性统计（属性为实体类型私有）
        if (types.Count == 0)
            return Ok(Array.Empty<EntityTypeSummaryDto>());

        var typeNames = types.Select(t => t.EntityType).ToList();
        var stats = await _db.AttributeCatalog
            .Where(a => typeNames.Contains(a.EntityType))
            .GroupBy(a => a.EntityType)
            .Select(g => new
            {
                EntityType = g.Key,
                Total = g.Count(),
                Searchable = g.Count(a => a.IsSearchable),
                FirstDisplay = g.OrderBy(a => a.DisplayOrder)
                                .Select(a => a.DisplayName)
                                .FirstOrDefault()
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var statsMap = stats.ToDictionary(s => s.EntityType);

        var result = types.Select(t =>
        {
            statsMap.TryGetValue(t.EntityType, out var s);
            return new EntityTypeSummaryDto(
                t.EntityType,
                s?.Total ?? 0,
                s?.Searchable ?? 0,
                s?.FirstDisplay,
                t.EntityTypeId,
                t.DisplayName);
        });

        return Ok(result);
    }

    // ============================================================
    // 批量详情（1 次 HTTP 替代 1+N 次）
    // ============================================================

    /// <summary>
    /// 批量返回所有实体类型详情（含属性计数）。
    ///
    /// ★ 为什么需要这个端点：
    ///   原 GET /api/eav/entity-types 只返回 SummaryDto，
    ///   前端要拿详情（Description / DisplayOrder / CreatedAt / UpdatedAt）
    ///   需逐个 GET /{id}，N 个类型 = 1+N 次 HTTP。
    ///   N=145 时前端 LoadAsync 严重卡顿（>15s）。
    ///
    /// 本端点：1 次 HTTP 返回全量详情 + 属性计数（GroupBy 聚合，1 次 DB 查询）。
    /// </summary>
    [HttpGet("details")]
    public async Task<IActionResult> ListDetails(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        // 1. 全量类型（1 次 DB）
        var query = _db.EntityTypes
            .Where(t => includeDeleted || !t.IsDeleted);

        var types = await query
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.EntityType)
            .AsNoTracking()
            .ToListAsync(ct);

        if (types.Count == 0)
            return Ok(Array.Empty<EntityTypeDetailDto>());

        // 2. 属性计数（1 次 DB，GroupBy 聚合）
        var typeNames = types.Select(t => t.EntityType).ToList();
        var stats = await _db.AttributeCatalog
            .Where(a => typeNames.Contains(a.EntityType))
            .GroupBy(a => a.EntityType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .AsNoTracking()
            .ToListAsync(ct);

        var statsMap = stats.ToDictionary(x => x.Type, x => x.Count);

        // 3. 组装
        var result = types.Select(t => new EntityTypeDetailDto(
            t.EntityTypeId,
            t.EntityType,
            t.DisplayName,
            t.Description,
            t.DisplayOrder,
            t.IsDeleted,
            t.CreatedAt,
            t.UpdatedAt,
            statsMap.GetValueOrDefault(t.EntityType, 0)));

        return Ok(result);
    }

    // ============================================================
    // 详情
    // ============================================================

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var t = await _db.EntityTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EntityTypeId == id, ct);
        if (t is null) return NotFound();

        var attrCount = await _db.AttributeCatalog
            .CountAsync(a => a.EntityType == t.EntityType, ct);

        return Ok(new EntityTypeDetailDto(
            t.EntityTypeId, t.EntityType, t.DisplayName, t.Description,
            t.DisplayOrder, t.IsDeleted, t.CreatedAt, t.UpdatedAt, attrCount));
    }

    // ============================================================
    // 创建
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateEntityTypeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest(new { error = "显示名必填" });

        // 生成或使用客户端指定的 EntityType
        string entityType;
        if (!string.IsNullOrWhiteSpace(req.EntityType))
        {
            entityType = req.EntityType.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    entityType, @"^[A-Za-z][A-Za-z0-9_]*$"))
                return BadRequest(new
                {
                    error = "EntityType 必须以字母开头，只含字母、数字、下划线"
                });
        }
        else
        {
            entityType = GenerateEntityTypeId();
        }

        // 唯一性检查（自动生成时最多重试 3 次防碰撞）；
        // 与 DB partial unique 索引 uq_entity_type（is_deleted = false）对齐：软删行不占名
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var exists = await _db.EntityTypes
                .AnyAsync(t => t.EntityType == entityType && !t.IsDeleted, ct);
            if (!exists) break;

            // 客户端显式指定的冲突 → 直接报错，不重试
            if (!string.IsNullOrWhiteSpace(req.EntityType))
                return Conflict(new { error = $"实体类型已存在: {entityType}" });

            // 自动生成的碰撞 → 重新生成
            entityType = GenerateEntityTypeId();
        }

        var entity = new EntityTypeDefinition
        {
            EntityType = entityType,
            DisplayName = req.DisplayName.Trim(),
            Description = req.Description,
            DisplayOrder = req.DisplayOrder
        };
        _db.EntityTypes.Add(entity);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // 并发兜底：uq_entity_type 唯一索引冲突（检查与写入之间存在竞态窗口）
            return Conflict(new { error = $"实体类型已存在: {entityType}" });
        }

        return Ok(new { entity.EntityTypeId, entity.EntityType });
    }

    /// <summary>
    /// 生成实体类型内部标识：`et_` + 12 位随机 hex（如 et_3f9a2b1c8d4e）。
    /// 12 位 hex = 48 bit 随机，碰撞概率可忽略（10 万个类型下 ≈ 1.8e-7）。
    /// </summary>
    private static string GenerateEntityTypeId()
        => "et_" + Guid.NewGuid().ToString("N")[..12];

    // ============================================================
    // 更新
    // ============================================================

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id, [FromBody] UpdateEntityTypeRequest req, CancellationToken ct)
    {
        var e = await _db.EntityTypes.FindAsync(new object[] { id }, ct);
        if (e is null || e.IsDeleted) return NotFound();

        if (req.DisplayName is not null) e.DisplayName = req.DisplayName.Trim();
        if (req.Description is not null) e.Description = req.Description;
        if (req.DisplayOrder is not null) e.DisplayOrder = req.DisplayOrder.Value;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ============================================================
    // 软删除
    // ============================================================

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var e = await _db.EntityTypes.FindAsync(new object[] { id }, ct);
        if (e is null || e.IsDeleted) return NotFound();

        // 仍有属性时拒绝
        var hasAttrs = await _db.AttributeCatalog
            .AnyAsync(a => a.EntityType == e.EntityType, ct);
        if (hasAttrs)
            return BadRequest(new
            {
                error = $"该实体类型仍有属性（{e.EntityType}），请先删除其属性"
            });

        // 子项为空方可删除本项（不采用级联删除）：该类型已产生实体实例时拒绝
        // 属性值/审计已无类型列，实例归属以 inode_entity 映射与自定义表行为准
        var hasEntities = await _db.InodeEntities
                .AnyAsync(m => m.EntityType == e.EntityType, ct)
            || await _db.CustomTableRows
                .AnyAsync(r => r.ParentEntityType == e.EntityType, ct);
        if (hasEntities)
            return BadRequest(new
            {
                error = $"该实体类型已产生实体实例（{e.EntityType}），请先删除其实例数据"
            });

        // 被节点声明引用时拒绝：否则节点上的类型卡片会指向已删除的类型
        var hasDeclarations = await _db.InodeEntityTypes
            .AnyAsync(d => d.EntityType == e.EntityType, ct);
        if (hasDeclarations)
            return BadRequest(new
            {
                error = $"该实体类型已被节点声明引用（{e.EntityType}），请先解除节点上的类型声明"
            });

        // 硬删除：物理移除
        _db.EntityTypes.Remove(e);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Api.Services;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Controllers;

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
    private readonly EavDbContext _db;
    private readonly IAttributeCache _attrCache;

    public EavEntityTypesController(EavDbContext db, IAttributeCache attrCache)
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
        // 1. 从 EntityTypeCatalog 拿所有活动类型
        var types = await _db.EntityTypes
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.EntityType)
            .AsNoTracking()
            .ToListAsync(ct);

        if (types.Count == 0)
            return Ok(Array.Empty<EntityTypeSummaryDto>());

        // 2. 从 AttributeCatalog 聚合统计
        var typeNames = types.Select(t => t.EntityType).ToList();
        var stats = await _db.AttributeCatalog
            .Where(a => !a.IsDeleted && typeNames.Contains(a.EntityType))
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
            .CountAsync(a => a.EntityType == t.EntityType && !a.IsDeleted, ct);

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

        // 唯一性检查（自动生成时最多重试 3 次防碰撞）
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var exists = await _db.EntityTypes
                .AnyAsync(t => t.EntityType == entityType, ct);
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
        await _db.SaveChangesAsync(ct);

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

        // 仍有活动属性时拒绝
        var hasAttrs = await _db.AttributeCatalog
            .AnyAsync(a => a.EntityType == e.EntityType && !a.IsDeleted, ct);
        if (hasAttrs)
            return BadRequest(new
            {
                error = $"该实体类型仍有活动属性（{e.EntityType}），请先删除其属性"
            });

        e.IsDeleted = true;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ============================================================
    // 恢复
    // ============================================================

    [HttpPost("{id}/undelete")]
    public async Task<IActionResult> Undelete(string id, CancellationToken ct)
    {
        var e = await _db.EntityTypes.FindAsync(new object[] { id }, ct);
        if (e is null) return NotFound();
        if (!e.IsDeleted) return NoContent();   // 幂等

        var conflict = await _db.EntityTypes.AnyAsync(
            t => t.EntityTypeId != id
              && t.EntityType == e.EntityType
              && !t.IsDeleted, ct);
        if (conflict)
            return Conflict(new
            {
                error = $"同名的活动实体类型已存在（{e.EntityType}）"
            });

        e.IsDeleted = false;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

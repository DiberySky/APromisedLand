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
/// 数据源为 EntityTypeCatalog 表（DisplayName / Description / DisplayOrder）。
/// ★ 属性已全局化（attribute_catalog 不再有 entity_type 维度），
///   类型不再拥有属性，摘要/详情中的属性计数固定为 0。
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
        var types = await _db.EntityTypes
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.EntityType)
            .AsNoTracking()
            .ToListAsync(ct);

        // 属性全局化后不再归属类型，计数固定为 0
        var result = types.Select(t => new EntityTypeSummaryDto(
            t.EntityType,
            0,
            0,
            null,
            t.EntityTypeId,
            t.DisplayName));

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
    /// 本端点：1 次 HTTP 返回全量详情。
    /// ★ 属性全局化后类型不再拥有属性，AttributeCount 固定为 0。
    /// </summary>
    [HttpGet("details")]
    public async Task<IActionResult> ListDetails(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var types = await _db.EntityTypes
            .Where(t => includeDeleted || !t.IsDeleted)
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.EntityType)
            .AsNoTracking()
            .ToListAsync(ct);

        var result = types.Select(t => new EntityTypeDetailDto(
            t.EntityTypeId,
            t.EntityType,
            t.DisplayName,
            t.Description,
            t.DisplayOrder,
            t.IsDeleted,
            t.CreatedAt,
            t.UpdatedAt,
            0));

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

        // 属性全局化后不再归属类型，AttributeCount 固定为 0
        return Ok(new EntityTypeDetailDto(
            t.EntityTypeId, t.EntityType, t.DisplayName, t.Description,
            t.DisplayOrder, t.IsDeleted, t.CreatedAt, t.UpdatedAt, 0));
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

        // ★ 属性/属性值已全局化（不再携带 entity_type 维度），不再拦截删除；
        //   实例归属由宿主 Id 全局唯一 GUID 表达。

        // 子项为空方可删除本项（不采用级联删除）：该类型已产生实体实例时拒绝
        var hasEntities = await _db.CustomTableRows
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

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.NodeEavSky.Controllers;

[ApiController]
[Route("api/eav/metadata/option-sets")]
public class OptionSetsController : ControllerBase
{
    private readonly EavDbContext _db;
    private readonly IOptionSetCache _optionSetCache;

    public OptionSetsController(EavDbContext db, IOptionSetCache optionSetCache)
    {
        _db = db;
        _optionSetCache = optionSetCache;
    }

    /// <summary>创建选项集</summary>
    [HttpPost]
    public async Task<IActionResult> CreateSet(
        [FromBody] CreateOptionSetRequest req, CancellationToken ct)
    {
        var set = new OptionSet
        {
            EntityType = req.EntityType,
            SetName = req.SetName,
            DisplayName = req.DisplayName
        };
        _db.OptionSets.Add(set);
        await _db.SaveChangesAsync(ct);

        return Ok(new { set.OptionSetId });
    }

    /// <summary>
    /// 查询选项集列表。
    /// ★ 新增 includeDeleted：管理页可勾选"显示已删除"以提供恢复入口。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? entityType,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        using var scope = HttpContext.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();

        var query = db.OptionSets.AsQueryable();
        if (!includeDeleted) query = query.Where(s => !s.IsDeleted);
        if (entityType is not null)
            query = query.Where(s => s.EntityType == entityType || s.EntityType == "Shared");

        var list = await query
            .OrderBy(s => s.OptionSetId)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(list.Select(s => new OptionSetSummaryDto(
            s.OptionSetId, s.EntityType, s.SetName, s.DisplayName, s.IsDeleted)));
    }

    /// <summary>
    /// 按 ID 获取选项集详情（含未删除的选项项）。
    /// ★ 新增 includeDeleted：默认过滤已删除集合（返回 404）；
    ///   恢复流程 / 管理页可通过 includeDeleted=true 读取。
    /// </summary>
    [HttpGet("{setId}")]
    public async Task<IActionResult> GetSet(
        string setId,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var set = await _db.OptionSets
            .Include(s => s.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OptionSetId == setId, ct);

        if (set is null) return NotFound();

        // ★ 已删集合：默认 404（与 GetAll 的默认过滤一致）
        if (set.IsDeleted && !includeDeleted) return NotFound();

        return Ok(ToOptionSetDetailDto(set));
    }

    /// <summary>
    /// 更新选项集基本信息。SetName 和 EntityType 不可修改（它们是引用标识），
    /// 只允许修改 DisplayName。
    /// </summary>
    [HttpPut("{setId}")]
    public async Task<IActionResult> UpdateSet(
        string setId, [FromBody] UpdateOptionSetRequest req, CancellationToken ct)
    {
        var set = await _db.OptionSets.FindAsync(new object[] { setId }, ct);
        if (set is null) return NotFound();

        if (req.DisplayName is not null) set.DisplayName = req.DisplayName;
        set.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _optionSetCache.Invalidate(setId);

        return NoContent();
    }

    /// <summary>查询选项集被哪些属性引用（删除前检查用）</summary>
    [HttpGet("{setId}/references")]
    public async Task<IActionResult> GetReferences(string setId, CancellationToken ct)
    {
        var refs = await _db.AttributeCatalog
            .Where(a => a.RefOptionSetId == setId && !a.IsDeleted)
            .Select(a => new OptionSetReferenceDto(
                a.AttributeId, a.EntityType, a.AttributeName, a.DisplayName))
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(refs);
    }

    /// <summary>
    /// ★ 软删除选项集：集合 + 全部选项都标记为删除。
    ///
    /// 语义：
    ///   - 被属性引用时仍拒绝删除（保守策略）。
    ///   - 读取端 `OptionSetCache.GetSet` 不过滤 IsDeleted，历史数据仍可拿到 Label。
    ///   - 唯一索引（entity_type, set_name）改为 partial（仅 is_deleted = false），
    ///     软删后同名集合可被重新创建。
    /// </summary>
    [HttpDelete("{setId}")]
    public async Task<IActionResult> DeleteSet(string setId, CancellationToken ct)
    {
        var set = await _db.OptionSets.FindAsync(new object[] { setId }, ct);
        if (set is null || set.IsDeleted) return NotFound();

        // 检查是否被属性引用（保持保守策略）
        var referenced = await _db.AttributeCatalog
            .AnyAsync(a => a.RefOptionSetId == setId && !a.IsDeleted, ct);
        if (referenced)
            return BadRequest(new { error = "该选项集仍被属性引用，无法删除。请先解除属性引用。" });

        // 软删所有选项
        var items = await _db.OptionItems.Where(i => i.OptionSetId == setId).ToListAsync(ct);
        foreach (var item in items)
        {
            item.IsDeleted = true;
            item.IsDefault = false;
        }

        // ★ 软删集合本身
        set.IsDeleted = true;
        set.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        _optionSetCache.Invalidate(setId);
        return NoContent();
    }

    /// <summary>
    /// ★ 恢复软删除的选项集（同时恢复全部选项）。
    ///
    /// 冲突：
    ///   - 若已存在同名活动集合 → 409（partial unique index 拒绝）
    ///   - 若某选项 Value 与活动项冲突 → 409，返回冲突列表
    /// 恢复后 IsDefault 一律清空（需显式重设）。
    /// </summary>
    [HttpPost("{setId}/undelete")]
    public async Task<IActionResult> UndeleteSet(string setId, CancellationToken ct)
    {
        var set = await _db.OptionSets.FindAsync(new object[] { setId }, ct);
        if (set is null) return NotFound();
        if (!set.IsDeleted) return NoContent();   // 幂等

        // 同名活动集合冲突
        var conflictSet = await _db.OptionSets.AnyAsync(
            s => s.OptionSetId != setId
              && s.EntityType == set.EntityType
              && s.SetName == set.SetName
              && !s.IsDeleted, ct);
        if (conflictSet)
            return Conflict(new
            {
                error = $"同名活动选项集已存在（{set.EntityType}/{set.SetName}），" +
                        "请先删除或改名后再恢复"
            });

        // 恢复集合本身
        set.IsDeleted = false;
        set.UpdatedAt = DateTimeOffset.UtcNow;

        // 恢复所有软删选项（清空 IsDefault）
        var deletedItems = await _db.OptionItems
            .Where(i => i.OptionSetId == setId && i.IsDeleted)
            .ToListAsync(ct);

        if (deletedItems.Count > 0)
        {
            // 冲突检查：Value 与活动项是否重复
            var restoredValues = deletedItems.Select(i => i.Value).ToList();
            var conflicts = await _db.OptionItems
                .Where(i => i.OptionSetId == setId
                         && !i.IsDeleted
                         && restoredValues.Contains(i.Value))
                .Select(i => i.Value)
                .ToListAsync(ct);

            if (conflicts.Count > 0)
                return Conflict(new
                {
                    error = $"以下选项的 Value 已存在活动记录，无法恢复: " +
                            string.Join(", ", conflicts),
                    conflictingValues = conflicts
                });

            foreach (var item in deletedItems)
            {
                item.IsDeleted = false;
                item.IsDefault = false;
            }
        }

        await _db.SaveChangesAsync(ct);
        _optionSetCache.Invalidate(setId);
        return NoContent();
    }

    // ---------- DTO 映射 ----------

    private static OptionSetDetailDto ToOptionSetDetailDto(OptionSet s) => new(
        s.OptionSetId,
        s.EntityType,
        s.SetName,
        s.DisplayName,
        s.CreatedAt,
        s.UpdatedAt,
        s.Items
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new OptionItemDetailDto(
                i.OptionItemId,
                i.Value,
                i.Label,
                i.DisplayOrder,
                i.IsDefault,
                i.IsDeleted,
                i.CreatedAt))
            .ToList(),
        s.IsDeleted);   // ★ 新增
}

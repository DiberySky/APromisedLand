using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Api.Services;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Controllers;

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

    /// <summary>查询选项集列表（可按实体类型过滤，含 Shared 共享集）</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? entityType, CancellationToken ct)
    {
        var list = await _db.OptionSets
            .Where(s => entityType == null
                     || s.EntityType == entityType || s.EntityType == "Shared")
            .OrderBy(s => s.OptionSetId)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(list.Select(s => new { s.OptionSetId, s.EntityType, s.SetName, s.DisplayName }));
    }

    /// <summary>获取选项集详情（含所有未删除的选项）</summary>
    [HttpGet("{setId:long}")]
    public async Task<IActionResult> GetSet(long setId, CancellationToken ct)
    {
        var set = await _db.OptionSets
            .Include(s => s.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OptionSetId == setId, ct);

        if (set is null) return NotFound();
        return Ok(ToOptionSetDetailDto(set));
    }

    /// <summary>
    /// 更新选项集基本信息。SetName 和 EntityType 不可修改（它们是引用标识），
    /// 只允许修改 DisplayName。
    /// </summary>
    [HttpPut("{setId:long}")]
    public async Task<IActionResult> UpdateSet(
        long setId, [FromBody] UpdateOptionSetRequest req, CancellationToken ct)
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
    [HttpGet("{setId:long}/references")]
    public async Task<IActionResult> GetReferences(long setId, CancellationToken ct)
    {
        var refs = await _db.AttributeCatalog
            .Where(a => a.RefOptionSetId == setId && !a.IsDeleted)
            .Select(a => new OptionSetReferenceDto(
                a.AttributeId, a.EntityType, a.AttributeName, a.DisplayName))
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(refs);
    }

    /// <summary>删除选项集（软删除全部选项 + 物理移除集合）</summary>
    [HttpDelete("{setId:long}")]
    public async Task<IActionResult> DeleteSet(long setId, CancellationToken ct)
    {
        var set = await _db.OptionSets.FindAsync(new object[] { setId }, ct);
        if (set is null) return NotFound();

        // 检查是否被属性引用（被引用时禁止删除，避免破坏已有数据）
        var referenced = await _db.AttributeCatalog
            .AnyAsync(a => a.RefOptionSetId == setId && !a.IsDeleted, ct);
        if (referenced)
            return BadRequest(new { error = "该选项集仍被属性引用，无法删除。请先解除属性引用。" });

        // 软删除全部选项（历史数据仍可降级显示 Value），再移除集合
        var items = await _db.OptionItems.Where(i => i.OptionSetId == setId).ToListAsync(ct);
        foreach (var item in items) { item.IsDeleted = true; item.IsDefault = false; }
        await _db.SaveChangesAsync(ct);

        _db.OptionSets.Remove(set);
        await _db.SaveChangesAsync(ct);

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
            .ToList());
}

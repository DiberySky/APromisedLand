using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Controllers;

[ApiController]
[Route("api/eav/metadata/option-sets")]
public class OptionSetsController : ControllerBase
{
    private readonly TreeGraphDbContext _db;
    private readonly IOptionSetCache _optionSetCache;

    public OptionSetsController(TreeGraphDbContext db, IOptionSetCache optionSetCache)
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
        var db = scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();

        var query = db.OptionSets.AsQueryable();
        if (!includeDeleted) query = query.Where(s => !s.IsDeleted);
        if (entityType is not null)
            query = query.Where(s => s.EntityType == entityType);

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
            .Where(a => a.RefOptionSetId == setId)
            .Select(a => new OptionSetReferenceDto(
                a.AttributeId, a.EntityType, a.AttributeName, a.DisplayName))
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(refs);
    }

    /// <summary>
    /// ★ 硬删除选项集（物理移除）。
    ///
    /// 语义：
    ///   - 被属性引用时拒绝删除（保守策略）。
    ///   - 子项为空方可删除本项：仍有活动选项时拒绝（不采用级联删除）。
    /// </summary>
    [HttpDelete("{setId}")]
    public async Task<IActionResult> DeleteSet(string setId, CancellationToken ct)
    {
        var set = await _db.OptionSets
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.OptionSetId == setId, ct);
        if (set is null || set.IsDeleted) return NotFound();

        // 检查是否被属性引用（保持保守策略）
        var referenced = await _db.AttributeCatalog
            .AnyAsync(a => a.RefOptionSetId == setId, ct);
        if (referenced)
            return BadRequest(new { error = "该选项集仍被属性引用，无法删除。请先解除属性引用。" });

        // 子项为空方可删除本集合：仍有活动选项时拒绝
        var hasItems = await _db.OptionItems
            .AnyAsync(i => i.OptionSetId == setId && !i.IsDeleted, ct);
        if (hasItems)
            return BadRequest(new { error = "该选项集仍有选项，无法删除。请先删除全部选项。" });

        // 硬删除：物理移除（遗留的软删除选项行一并清掉）
        _db.OptionItems.RemoveRange(set.Items);
        _db.OptionSets.Remove(set);

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

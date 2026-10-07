using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.NodeEav.Dtos;

namespace TreeGraph.Api.NodeEavSky.Controllers;

[ApiController]
[Route("api/eav/metadata/option-sets/{setId}/items")]
public class OptionItemsController : ControllerBase
{
    private readonly TreeGraphDbContext _db;
    private readonly IOptionSetCache _optionSetCache;

    public OptionItemsController(TreeGraphDbContext db, IOptionSetCache optionSetCache)
    {
        _db = db;
        _optionSetCache = optionSetCache;
    }

    /// <summary>列出选项集内所有选项（含已软删除的，供管理员查看）</summary>
    [HttpGet]
    public async Task<IActionResult> ListItems(string setId, CancellationToken ct)
    {
        var setExists = await _db.OptionSets
            .AnyAsync(s => s.OptionSetId == setId, ct);
        if (!setExists) return NotFound();

        // 不按 IsDeleted 过滤，前端根据状态展示"已删除"标签
        var items = await _db.OptionItems
            .Where(i => i.OptionSetId == setId)
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.OptionItemId)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(items.Select(i => new OptionItemDetailDto(
            i.OptionItemId,
            i.Value,
            i.Label,
            i.DisplayOrder,
            i.IsDefault,
            i.IsDeleted,
            i.CreatedAt)));
    }

    /// <summary>添加选项</summary>
    [HttpPost]
    public async Task<IActionResult> AddOption(
        string setId, [FromBody] CreateOptionItemRequest req, CancellationToken ct)
    {
        var item = new OptionItem
        {
            OptionSetId = setId,
            Value = req.Value,
            Label = req.Label,
            DisplayOrder = req.DisplayOrder,
            IsDefault = req.IsDefault
        };

        if (req.IsDefault)
        {
            // 一个集合内只有一个默认：先清掉其它默认
            await _db.OptionItems
                .Where(i => i.OptionSetId == setId && i.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsDefault, false), ct);
        }

        _db.OptionItems.Add(item);
        await _db.SaveChangesAsync(ct);
        _optionSetCache.Invalidate(setId);

        return Ok(new { item.OptionItemId });
    }

    /// <summary>更新选项（可改 Value 之外的 Label、顺序、默认标记；改 Label 不影响已存数据）</summary>
    [HttpPut("{itemId}")]
    public async Task<IActionResult> UpdateOption(
        string setId, string itemId, [FromBody] UpdateOptionItemRequest req,
        CancellationToken ct)
    {
        var item = await _db.OptionItems
            .FirstOrDefaultAsync(i => i.OptionItemId == itemId && i.OptionSetId == setId, ct);
        if (item is null) return NotFound();

        if (req.Label is not null) item.Label = req.Label;
        if (req.DisplayOrder is not null) item.DisplayOrder = req.DisplayOrder.Value;

        if (req.IsDefault is true)
        {
            await _db.OptionItems
                .Where(i => i.OptionSetId == setId && i.IsDefault && i.OptionItemId != itemId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsDefault, false), ct);
            item.IsDefault = true;
        }
        else if (req.IsDefault is false)
        {
            item.IsDefault = false;
        }

        await _db.SaveChangesAsync(ct);
        _optionSetCache.Invalidate(setId);

        return NoContent();
    }

    /// <summary>删除选项（软删除：历史数据仍存有旧 Value，读取端降级显示）</summary>
    [HttpDelete("{itemId}")]
    public async Task<IActionResult> DeleteOption(
        string setId, string itemId, CancellationToken ct)
    {
        var item = await _db.OptionItems
            .FirstOrDefaultAsync(i => i.OptionItemId == itemId && i.OptionSetId == setId, ct);
        if (item is null) return NotFound();

        item.IsDeleted = true;
        item.IsDefault = false;
        await _db.SaveChangesAsync(ct);
        _optionSetCache.Invalidate(setId);

        return NoContent();
    }

    /// <summary>选项重排序</summary>
    [HttpPut("reorder")]
    public async Task<IActionResult> ReorderOptions(
        string setId, [FromBody] List<ReorderOptionItem> items, CancellationToken ct)
    {
        var ids = items.Select(i => i.OptionItemId).ToList();
        var existing = await _db.OptionItems
            .Where(i => i.OptionSetId == setId && ids.Contains(i.OptionItemId))
            .ToListAsync(ct);

        foreach (var item in existing)
        {
            var order = items.First(i => i.OptionItemId == item.OptionItemId).DisplayOrder;
            item.DisplayOrder = order;
        }
        await _db.SaveChangesAsync(ct);
        _optionSetCache.Invalidate(setId);

        return NoContent();
    }

    /// <summary>
    /// ★ 恢复被软删除的选项项。
    /// 唯一约束（option_set_id, value）不区分 IsDeleted。
    /// </summary>
    [HttpPost("{itemId}/undelete")]
    public async Task<IActionResult> UndeleteOption(
        string setId, string itemId, CancellationToken ct)
    {
        var item = await _db.OptionItems
            .FirstOrDefaultAsync(i => i.OptionItemId == itemId
                                   && i.OptionSetId == setId, ct);
        if (item is null) return NotFound();
        if (!item.IsDeleted) return NoContent();

        var conflict = await _db.OptionItems.AnyAsync(
            i => i.OptionItemId != itemId
              && i.OptionSetId == setId
              && i.Value == item.Value
              && !i.IsDeleted, ct);

        if (conflict)
            return Conflict(new
            {
                error = $"同 Value 的活动选项已存在（{item.Value}）"
            });

        item.IsDeleted = false;
        item.IsDefault = false;
        await _db.SaveChangesAsync(ct);
        _optionSetCache.Invalidate(setId);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Api.Services;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Controllers;

[ApiController]
[Route("api/units")]
public class UnitsController : ControllerBase
{
    private readonly EavDbContext _db;
    private readonly IUnitCache _unitCache;

    public UnitsController(EavDbContext db, IUnitCache unitCache)
    {
        _db = db;
        _unitCache = unitCache;
    }

    /// <summary>获取全部单位（可按分类过滤）</summary>
    [HttpGet]
    public IActionResult GetAll([FromQuery] string? category)
    {
        var units = category is null
            ? _unitCache.GetAll()
            : _unitCache.GetByCategory(category);

        return Ok(units.Select(ToDto));
    }

    /// <summary>创建单位</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateUnitRequest req, CancellationToken ct)
    {
        var unit = new Unit
        {
            Id = Guid.NewGuid(),
            Category = req.Category,
            Name = req.Name,
            Symbol = req.Symbol,
            ToBaseFactor = req.ToBaseFactor,
            IsBaseUnit = req.IsBaseUnit,
            DisplayOrder = req.DisplayOrder
        };
        _db.Units.Add(unit);
        await _db.SaveChangesAsync(ct);
        _unitCache.Invalidate();

        return Ok(new { unit.Id });
    }

    /// <summary>更新单位（Name / Symbol / DisplayOrder / IsBaseUnit）。</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateUnitRequest req, CancellationToken ct)
    {
        var unit = await _db.Units.FindAsync(new object[] { id }, ct);
        if (unit is null || unit.IsDeleted) return NotFound();

        // ---- Name ----
        if (req.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { error = "名称不能为空" });

            var conflict = await _db.Units.AnyAsync(
                u => u.Id != id && u.Category == unit.Category && u.Name == req.Name, ct);
            if (conflict)
                return Conflict(new { error = $"同分类下已存在名称「{req.Name}」的单位" });

            unit.Name = req.Name;
        }

        // ---- Symbol ----
        if (req.Symbol is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Symbol))
                return BadRequest(new { error = "符号不能为空" });
            unit.Symbol = req.Symbol;
        }

        // ---- DisplayOrder ----
        if (req.DisplayOrder is not null)
            unit.DisplayOrder = req.DisplayOrder.Value;

        // ---- IsBaseUnit（分类级原子切换）----
        if (req.IsBaseUnit is bool desired && desired != unit.IsBaseUnit)
        {
            if (desired)
            {
                await _db.Units
                    .Where(u => u.Category == unit.Category
                             && u.IsBaseUnit
                             && u.Id != id)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(u => u.IsBaseUnit, false), ct);

                unit.IsBaseUnit = true;
            }
            else
            {
                return BadRequest(new
                {
                    error = "不能直接取消基准单位。请先在目标单位上勾选「基准单位」完成切换。"
                });
            }
        }

        unit.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _unitCache.Invalidate();

        return NoContent();
    }

    /// <summary>软删除单位。仍被属性或数值数据引用时拒绝。</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var unit = await _db.Units.FindAsync(new object[] { id }, ct);
        if (unit is null || unit.IsDeleted) return NotFound();

        var attrRefs = await _db.AttributeCatalog
            .Where(a => a.UnitId == id && !a.IsDeleted)
            .Select(a => new { a.AttributeId, a.EntityType, a.AttributeName, a.DisplayName })
            .ToListAsync(ct);

        if (attrRefs.Count > 0)
        {
            return BadRequest(new
            {
                error = $"该单位仍被 {attrRefs.Count} 个属性引用，无法删除",
                attributes = attrRefs
            });
        }

        var valueRefCount = await _db.AttributeValues.CountAsync(v => v.UnitId == id, ct);
        if (valueRefCount > 0)
        {
            return BadRequest(new
            {
                error = $"该单位仍被 {valueRefCount} 条数值数据的原始输入单位引用，无法删除",
                valueCount = valueRefCount
            });
        }

        unit.IsDeleted = true;
        unit.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _unitCache.Invalidate();

        return NoContent();
    }

    // ============================================================
    // ★ 新增：迁移分类
    // ============================================================

    /// <summary>
    /// 把单位迁移到其它分类。要求该单位未被任何属性绑定或数值数据引用为输入单位。
    /// ToBaseFactor 语义随分类变化，必须重设。
    /// </summary>
    [HttpPost("{id:guid}/migrate-category")]
    public async Task<IActionResult> MigrateCategory(
        Guid id, [FromBody] MigrateUnitCategoryRequest req, CancellationToken ct)
    {
        var unit = await _db.Units.FindAsync(new object[] { id }, ct);
        if (unit is null || unit.IsDeleted) return NotFound();

        if (string.IsNullOrWhiteSpace(req.NewCategory))
            return BadRequest(new { error = "新分类不能为空" });
        if (req.NewCategory == unit.Category)
            return BadRequest(new { error = "新分类与原分类相同" });
        if (req.NewToBaseFactor <= 0)
            return BadRequest(new { error = "新换算系数必须大于 0" });

        // 引用检查
        var attrRefs = await _db.AttributeCatalog
            .Where(a => a.UnitId == id && !a.IsDeleted)
            .Select(a => new { a.AttributeId, a.EntityType, a.AttributeName, a.DisplayName })
            .ToListAsync(ct);

        if (attrRefs.Count > 0)
        {
            return BadRequest(new
            {
                error = $"该单位被 {attrRefs.Count} 个属性绑定为基准单位，无法迁移。请先解除属性绑定。",
                attributes = attrRefs
            });
        }

        var valueRefCount = await _db.AttributeValues.CountAsync(v => v.UnitId == id, ct);
        if (valueRefCount > 0)
        {
            return BadRequest(new
            {
                error = $"该单位被 {valueRefCount} 条数值数据引用为输入单位，无法迁移。",
                valueCount = valueRefCount
            });
        }

        // 迁移基准单位时，新分类不能已有基准
        if (unit.IsBaseUnit)
        {
            var targetHasBase = await _db.Units
                .AnyAsync(u => u.Category == req.NewCategory
                            && u.IsBaseUnit
                            && !u.IsDeleted, ct);
            if (targetHasBase)
                return Conflict(new { error = $"分类「{req.NewCategory}」已有基准单位，无法迁移" });
        }

        // 新分类下同名冲突
        var nameConflict = await _db.Units
            .AnyAsync(u => u.Id != id
                        && u.Category == req.NewCategory
                        && u.Name == unit.Name, ct);
        if (nameConflict)
            return Conflict(new { error = $"分类「{req.NewCategory}」下已存在名称「{unit.Name}」的单位" });

        var oldCategory = unit.Category;

        unit.Category = req.NewCategory;
        unit.ToBaseFactor = req.NewToBaseFactor;
        unit.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _unitCache.Invalidate();

        return Ok(new { oldCategory, newCategory = unit.Category });
    }

    // ============================================================
    // ★ 新增：修改 ToBaseFactor 并重算数据
    // ============================================================

    /// <summary>
    /// 修改单位的 ToBaseFactor 并重算所有受影响的 AttributeValue。
    ///
    /// 公式（存储值 S = 输入值 V × F_input / F_base）：
    ///   - 该单位仅作输入单位：S_new = S_old × newF / oldF
    ///   - 该单位仅作基准单位：S_new = S_old × oldF / newF
    ///   - 同时命中两种角色：S 不变
    ///
    /// 按 ValueId 键集分页（每批 10000 行）、每批独立事务提交，避免一次性把全部
    /// 受影响行载入内存。代价是放弃跨批原子性：中途失败会留下部分批次已重算的
    /// 中间态。危险操作，建议先备份数据库。
    /// </summary>
    [HttpPost("{id:guid}/recalculate-factor")]
    public async Task<IActionResult> RecalculateFactor(
        Guid id, [FromBody] RecalculateUnitFactorRequest req, CancellationToken ct)
    {
        var unit = await _db.Units.FindAsync(new object[] { id }, ct);
        if (unit is null || unit.IsDeleted) return NotFound();

        if (req.NewToBaseFactor <= 0)
            return BadRequest(new { error = "新换算系数必须大于 0" });
        if (req.NewToBaseFactor == unit.ToBaseFactor)
        {
            return Ok(new RecalculateUnitFactorResult(
                0, 0, unit.ToBaseFactor, req.NewToBaseFactor));
        }

        var oldFactor = unit.ToBaseFactor;
        var newFactor = req.NewToBaseFactor;
        var ratio = newFactor / oldFactor;        // 输入单位角色
        var invRatio = oldFactor / newFactor;     // 基准单位角色

        // 基准单位角色：属性.UnitId == id 的属性 ID 集合。
        // 属性目录数量级小，仅加载 ID；具体值行通过下面的分批扫描处理。
        var baseAttrIds = await _db.AttributeCatalog
            .Where(a => a.UnitId == id && !a.IsDeleted)
            .Select(a => a.AttributeId)
            .ToListAsync(ct);
        var baseAttrSet = baseAttrIds.ToHashSet();

        // 受影响行的单路条件：作为输入单位（v.UnitId == id）
        // 或作为基准单位所绑定属性的值（v.AttributeId ∈ baseAttrIds）。
        IQueryable<AttributeValue> ApplyScope(IQueryable<AttributeValue> q) =>
            baseAttrIds.Count == 0
                ? q.Where(v => v.UnitId == id)
                : q.Where(v => v.UnitId == id || baseAttrIds.Contains(v.AttributeId));

        // 键集（keyset）分页：以 ValueId 为游标稳定推进，不偏移、不跳行、不把全部行载入内存。
        // 每批使用独立事务提交——分批处理放弃跨批原子性，若中途失败会留下"部分批次已重算"
        // 的中间态（本接口属危险操作，UI 已要求先备份）。
        const int batchSize = 10000;
        long lastId = 0;
        int affectedValues = 0;

        while (true)
        {
            // 1) no-tracking 读取本批原始行
            var batch = await ApplyScope(_db.AttributeValues.AsNoTracking())
                .Where(v => v.ValueId > lastId)
                .OrderBy(v => v.ValueId)
                .Take(batchSize)
                .ToListAsync(ct);
            if (batch.Count == 0) break;

            // 2) 基于刚读出的原始值，一次性预计算"绝对目标值"。
            //    双角色行（同时是输入单位 + 基准单位属性的值）值不变，不纳入目标集合。
            var targets = new Dictionary<long, (decimal? Dec, long? Int)>(batch.Count);
            foreach (var v in batch)
            {
                var isInput = v.UnitId == id;
                var isBase = baseAttrSet.Contains(v.AttributeId);
                if (isInput && isBase) continue;

                var factor = isInput ? ratio : invRatio;
                targets[v.ValueId] = (
                    v.ValueDecimal is { } d ? d * factor : null,
                    v.ValueInt is { } i ? (long?)Math.Round(i * factor) : null);
            }

            var batchIds = batch.Select(v => v.ValueId).ToList();
            var stamp = DateTimeOffset.UtcNow;

            // 3) 本批独立事务。委托内重新 tracking 加载并赋"绝对目标值"，
            //    使 EnableRetryOnFailure 重试时幂等——赋值与当前值无关，绝不做相对乘法。
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);

                var tracked = await _db.AttributeValues
                    .Where(v => batchIds.Contains(v.ValueId))
                    .ToListAsync(ct);

                foreach (var v in tracked)
                {
                    if (!targets.TryGetValue(v.ValueId, out var t)) continue;
                    if (t.Dec is { } nd) v.ValueDecimal = nd;
                    if (t.Int is { } ni) v.ValueInt = ni;
                    v.UpdatedAt = stamp;
                }

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });

            affectedValues += batch.Count;
            lastId = batch[^1].ValueId;
        }

        // 所有数据批次完成后再切换系数（单行更新）
        unit.ToBaseFactor = newFactor;
        unit.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _unitCache.Invalidate();

        return Ok(new RecalculateUnitFactorResult(
            affectedValues,
            baseAttrIds.Count,
            oldFactor,
            newFactor));
    }

    /// <summary>按分类分组，附基准单位信息</summary>
    [HttpGet("categories")]
    public IActionResult GetCategories()
    {
        var categories = _unitCache.GetAll()
            .GroupBy(u => u.Category)
            .Select(g => new UnitCategoryDto(
                g.Key,
                g.FirstOrDefault(u => u.IsBaseUnit) is { } b ? ToDto(b) : null,
                g.OrderBy(u => u.DisplayOrder).Select(ToDto).ToList()));
        return Ok(categories);
    }

    private static UnitDto ToDto(Unit u) => new(
        u.Id, u.Category, u.Name, u.Symbol,
        u.ToBaseFactor, u.IsBaseUnit, u.DisplayOrder);
}

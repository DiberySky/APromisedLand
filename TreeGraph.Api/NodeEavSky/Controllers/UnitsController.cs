using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.NodeEavSky.Controllers;

[ApiController]
[Route("api/units")]
public class UnitsController : ControllerBase
{
    private readonly TreeGraphDbContext _db;
    private readonly IUnitCache _unitCache;

    /// <summary>
    /// 单次同步重算的行数上限。超过时拒绝并建议离线处理。
    /// 取值依据：ExecuteUpdateAsync 处理 20 万行约 2–4s，
    /// 事务锁持有 &lt; 5s，稳定落在 Polly AttemptTimeout（30s）内。
    /// </summary>
    private const int SyncRecalculateLimit = 200_000;

    public UnitsController(TreeGraphDbContext db, IUnitCache unitCache)
    {
        _db = db;
        _unitCache = unitCache;
    }

    // ============================================================
    // 基础 CRUD
    // ============================================================

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
        if (string.IsNullOrWhiteSpace(req.Category) ||
            string.IsNullOrWhiteSpace(req.Name) ||
            string.IsNullOrWhiteSpace(req.Symbol))
            return BadRequest(new { error = "分类、名称、符号不能为空" });

        if (req.ToBaseFactor <= 0)
            return BadRequest(new { error = "换算系数必须大于 0" });

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

    /// <summary>
    /// 更新单位。Name / Symbol / DisplayOrder 直接改；
    /// IsBaseUnit 支持 false → true 的"升级为基准"（同分类其它单位自动降级），
    /// 但拒绝 true → false 的"取消基准"（会导致分类失去基准）。
    /// Category / ToBaseFactor 不可修改——请使用 migrate-category / recalculate-factor 端点。
    ///
    /// ★ 修复：基准单位切换包在事务中，避免先清空其它基准、再设新基准两步
    ///   之间崩溃导致分类失去基准单位。
    /// </summary>
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

            // 同分类下名称唯一（含软删除记录——数据库唯一索引不区分 IsDeleted）
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
        var needBaseSwitch = req.IsBaseUnit is bool desired && desired != unit.IsBaseUnit;

        if (needBaseSwitch && req.IsBaseUnit == false)
        {
            // true → false：拒绝，避免分类失去唯一基准
            return BadRequest(new
            {
                error = "不能直接取消基准单位。请先在目标单位上勾选「基准单位」完成切换。"
            });
        }

        if (needBaseSwitch)
        {
            // false → true：用事务包住「清空其它基准 + 设置新基准 + 提交」三步
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);

                // 绕过 Change Tracker 批量清空同分类其它单位的基准标记
                await _db.Units
                    .Where(u => u.Category == unit.Category
                             && u.IsBaseUnit
                             && u.Id != id)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(u => u.IsBaseUnit, false), ct);

                unit.IsBaseUnit = true;
                unit.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);

                await tx.CommitAsync(ct);
            });
        }
        else
        {
            unit.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        _unitCache.Invalidate();

        return NoContent();
    }

    /// <summary>
    /// 软删除单位。仍被属性定义或数值数据引用时拒绝。
    /// 若删除的是分类基准单位，该分类将失去基准单位（由调用方自行重建）。
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var unit = await _db.Units.FindAsync(new object[] { id }, ct);
        if (unit is null || unit.IsDeleted) return NotFound();

        // 1) 属性定义引用（基准单位）
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

        // 2) 数值数据的"原始输入单位"引用
        var valueRefCount = await _db.AttributeValues.CountAsync(v => v.UnitId == id, ct);
        if (valueRefCount > 0)
        {
            return BadRequest(new
            {
                error = $"该单位仍被 {valueRefCount} 条数值数据的原始输入单位引用，无法删除",
                valueCount = valueRefCount
            });
        }

        // ★ 3) 组合字段引用
        var compositeFieldRefs = await _db.CompositeFields
            .Where(f => f.UnitId == id && !f.IsDeleted)
            .Select(f => new { f.FieldId, f.FieldName, f.CompositeTypeId })
            .ToListAsync(ct);

        if (compositeFieldRefs.Count > 0)
        {
            return BadRequest(new
            {
                error = $"该单位仍被 {compositeFieldRefs.Count} 个组合字段引用，无法删除",
                fields = compositeFieldRefs
            });
        }

        unit.IsDeleted = true;
        unit.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _unitCache.Invalidate();

        return NoContent();
    }

    // ============================================================
    // 迁移分类
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

        // 引用检查：被属性绑定
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

        // 引用检查：被数值数据引用为输入单位
        var valueRefCount = await _db.AttributeValues.CountAsync(v => v.UnitId == id, ct);
        if (valueRefCount > 0)
        {
            return BadRequest(new
            {
                error = $"该单位被 {valueRefCount} 条数值数据引用为输入单位，无法迁移。",
                valueCount = valueRefCount
            });
        }

        // ★ 组合字段引用
        var compositeFieldRefs = await _db.CompositeFields
            .Where(f => f.UnitId == id && !f.IsDeleted)
            .Select(f => new { f.FieldId, f.FieldName, f.CompositeTypeId })
            .ToListAsync(ct);

        if (compositeFieldRefs.Count > 0)
        {
            return BadRequest(new
            {
                error = $"该单位被 {compositeFieldRefs.Count} 个组合字段引用，无法迁移。请先解除组合字段的单位绑定。",
                fields = compositeFieldRefs
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
    // 重算换算系数（数据库端批量 UPDATE）
    // ============================================================

    /// <summary>
    /// 修改单位 ToBaseFactor 并重算所有受影响的 AttributeValue。
    ///
    /// 用 EF Core 的 ExecuteUpdateAsync 生成数据库端 UPDATE：
    ///   1) 仅作为输入单位的行：S_new = S_old × newF / oldF
    ///   2) 仅作为基准单位的行：S_new = S_old × oldF / newF
    ///   3) 单位本身的 ToBaseFactor
    ///
    /// 两种角色叠加的行（v.UnitId == id 且其属性绑该单位）不更新——值不变。
    ///
    /// 数值处理说明：
    ///   - ValueDecimal 按公式重算（numeric(38,15)，精度充足）
    ///   - ValueInt 保持不变。原因：int 类型属性绑单位极罕见，且乘除后
    ///     若非整数会被截断，静默丢精度不可接受。如确有需要，请改用 decimal。
    ///
    /// ★ 修复：若该单位被组合字段引用，拒绝此操作。原因：组合内数据存于 JSONB，
    ///   当前的批量 UPDATE 只处理 AttributeValue 表，无法安全重算 JSONB 内的数值。
    ///   需要重算时请先解除组合字段的单位绑定，或执行离线迁移脚本。
    ///
    /// 阈值保护：估算影响行数 &gt; <see cref="SyncRecalculateLimit"/> 时拒绝。
    /// 事务：ExecutionStrategy 包裹 BeginTransaction + 3 条 ExecuteUpdate，
    /// 兼容 Aspire Npgsql 的 EnableRetryOnFailure。
    /// </summary>
    [HttpPost("{id:guid}/recalculate-factor")]
    public async Task<IActionResult> RecalculateFactor(
        Guid id, [FromBody] RecalculateUnitFactorRequest req, CancellationToken ct)
    {
        var unit = await _db.Units.FindAsync(new object[] { id }, ct);
        if (unit is null || unit.IsDeleted) return NotFound();

        if (req.NewToBaseFactor <= 0)
            return BadRequest(new { error = "新换算系数必须大于 0" });

        // ★ 组合字段引用检查
        var compositeFieldRefCount = await _db.CompositeFields
            .CountAsync(f => f.UnitId == id && !f.IsDeleted, ct);
        if (compositeFieldRefCount > 0)
        {
            return BadRequest(new
            {
                error = $"该单位被 {compositeFieldRefCount} 个组合字段引用，" +
                        "重算系数暂不支持组合内数据（JSONB 内数值不在此端点重算范围内）。" +
                        "请先解除组合字段的单位绑定，或执行离线迁移脚本。",
                compositeFieldCount = compositeFieldRefCount
            });
        }

        if (req.NewToBaseFactor == unit.ToBaseFactor)
        {
            return Ok(new RecalculateUnitFactorResult(
                0, 0, unit.ToBaseFactor, req.NewToBaseFactor));
        }

        var oldFactor = unit.ToBaseFactor;
        var newFactor = req.NewToBaseFactor;

        // ---- 1. 前置估算（3 条 COUNT，不加载数据）----
        var baseAttrCount = await _db.AttributeCatalog
            .CountAsync(a => a.UnitId == id && !a.IsDeleted, ct);

        var aCount = await _db.AttributeValues
            .CountAsync(v => v.UnitId == id, ct);

        var bCount = 0;
        var overlapCount = 0;
        if (baseAttrCount > 0)
        {
            bCount = await _db.AttributeValues
                .CountAsync(v => _db.AttributeCatalog.Any(
                    a => a.AttributeId == v.AttributeId
                      && a.UnitId == id
                      && !a.IsDeleted), ct);

            overlapCount = await _db.AttributeValues
                .CountAsync(v => v.UnitId == id
                              && _db.AttributeCatalog.Any(
                                     a => a.AttributeId == v.AttributeId
                                       && a.UnitId == id
                                       && !a.IsDeleted), ct);
        }

        // 实际更新行数 = |A \ B| + |B \ A| = |A| + |B| − 2·|A ∩ B|
        var estimatedRows = aCount + bCount - 2 * overlapCount;

        if (estimatedRows > SyncRecalculateLimit)
        {
            return BadRequest(new
            {
                error = $"预计影响 {estimatedRows} 条数据，超过单次同步阈值 " +
                        $"{SyncRecalculateLimit}。请分批处理，或联系管理员执行离线迁移脚本。",
                estimatedRows,
                syncLimit = SyncRecalculateLimit
            });
        }

        // ---- 2. 执行批量更新 ----
        var ratio = newFactor / oldFactor;        // 输入单位角色
        var invRatio = oldFactor / newFactor;     // 基准单位角色
        var now = DateTimeOffset.UtcNow;

        var strategy = _db.Database.CreateExecutionStrategy();

        int affectedInput = 0;
        int affectedBase = 0;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // 2.1) 仅作为输入单位：v.UnitId == id 且其属性未绑定该单位
            affectedInput = await _db.AttributeValues
                .Where(v => v.UnitId == id
                         && !_db.AttributeCatalog.Any(
                                a => a.AttributeId == v.AttributeId
                                  && a.UnitId == id
                                  && !a.IsDeleted))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(v => v.ValueDecimal,
                        v => v.ValueDecimal * ratio)
                    .SetProperty(v => v.UpdatedAt, now), ct);

            // 2.2) 仅作为基准单位：属性绑该单位 且 v.UnitId != id
            affectedBase = await _db.AttributeValues
                .Where(v => v.UnitId != id
                         && _db.AttributeCatalog.Any(
                                a => a.AttributeId == v.AttributeId
                                  && a.UnitId == id
                                  && !a.IsDeleted))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(v => v.ValueDecimal,
                        v => v.ValueDecimal * invRatio)
                    .SetProperty(v => v.UpdatedAt, now), ct);

            // 2.3) 更新单位本身
            await _db.Units
                .Where(u => u.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.ToBaseFactor, newFactor)
                    .SetProperty(u => u.UpdatedAt, now), ct);

            await tx.CommitAsync(ct);
        });

        _unitCache.Invalidate();

        return Ok(new RecalculateUnitFactorResult(
            affectedInput + affectedBase,
            baseAttrCount,
            oldFactor,
            newFactor));
    }

    // ============================================================
    // 分类视图
    // ============================================================

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

    // ============================================================
    // 辅助
    // ============================================================

    private static UnitDto ToDto(Unit u) => new(
        u.Id, u.Category, u.Name, u.Symbol,
        u.ToBaseFactor, u.IsBaseUnit, u.DisplayOrder);
}
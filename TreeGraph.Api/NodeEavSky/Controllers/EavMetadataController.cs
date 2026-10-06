using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.NodeEavSky.Controllers;

[ApiController]
[Route("api/eav/metadata")]
public class EavMetadataController : ControllerBase
{
    private readonly TreeGraphDbContext _db;
    private readonly IAttributeCache _attrCache;
    private readonly ICompositeTypeCache _compositeCache;
    private readonly ICustomTableCache _customTableCache;

    public EavMetadataController(
        TreeGraphDbContext db, IAttributeCache attrCache, ICompositeTypeCache compositeCache,
        ICustomTableCache customTableCache)
    {
        _db = db;
        _attrCache = attrCache;
        _compositeCache = compositeCache;
        _customTableCache = customTableCache;
    }

    // ============================================================
    // 属性定义
    // ============================================================

    /// <summary>
    /// 创建属性定义。
    ///
    /// AttributeName 可选：
    ///   - null / 空：自动生成 `attr_` + 12 位 hex
    ///   - 非空：必须字母开头，字母/数字/下划线，且同一 EntityType 下唯一
    ///
    /// int / decimal 均可绑定单位（原 int 拒绝逻辑已移除）。
    /// </summary>
    [HttpPost("attributes")]
    public async Task<IActionResult> CreateAttribute(
        [FromBody] CreateAttributeRequest req, CancellationToken ct)
    {
        // ---- 基础校验 ----
        if (string.IsNullOrWhiteSpace(req.EntityType))
            return BadRequest(new { error = "EntityType 必填" });
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest(new { error = "DisplayName 必填" });
        if (!EavDataTypes.All.Contains(req.DataType))
            return BadRequest(new { error = $"未知的 dataType: {req.DataType}" });

        // ---- 实体类型存在性 ----
        var entityTypeExists = await _db.EntityTypes
            .AnyAsync(t => t.EntityType == req.EntityType && !t.IsDeleted, ct);
        if (!entityTypeExists)
            return BadRequest(new
            {
                error = $"实体类型不存在: {req.EntityType}。请先在「实体类型管理」中创建"
            });

        // ---- 引用互斥 ----
        var refCount = new[] {
            req.RefCompositeTypeId, req.RefTableDefinitionId, req.RefOptionSetId
        }.Count(x => x is not null);
        if (refCount > 1)
            return BadRequest(new { error = "组合类型 / 自定义表 / 选项集引用互斥" });

        // ---- 类型与引用匹配 ----
        if (req.DataType == EavDataTypes.Composite && req.RefCompositeTypeId is null)
            return BadRequest(new { error = "composite 必须指定 refCompositeTypeId" });
        if (req.DataType == EavDataTypes.Table && req.RefTableDefinitionId is null)
            return BadRequest(new { error = "table 必须指定 refTableDefinitionId" });
        if (req.DataType == EavDataTypes.SingleChoice && req.RefOptionSetId is null)
            return BadRequest(new { error = "single_choice 必须指定 refOptionSetId" });
        if (req.DataType is not (EavDataTypes.Composite or EavDataTypes.Table
            or EavDataTypes.SingleChoice) && refCount > 0)
            return BadRequest(new { error = "当前 dataType 不支持引用" });

        // ---- 引用存在性 ----
        if (req.UnitId is { } uid
            && !await _db.Units.AnyAsync(u => u.Id == uid && !u.IsDeleted, ct))
            return BadRequest(new { error = $"单位不存在: {uid}" });
        if (req.RefCompositeTypeId is { } cid
            && !await _db.CompositeTypes.AnyAsync(
                t => t.CompositeTypeId == cid && !t.IsDeleted, ct))
            return BadRequest(new { error = $"组合类型不存在: {cid}" });
        if (req.RefTableDefinitionId is { } tid
            && !await _db.CustomTables.AnyAsync(
                t => t.TableDefinitionId == tid && !t.IsDeleted, ct))
            return BadRequest(new { error = $"自定义表不存在: {tid}" });
        if (req.RefOptionSetId is { } sid
            && !await _db.OptionSets.AnyAsync(s => s.OptionSetId == sid, ct))
            return BadRequest(new { error = $"选项集不存在: {sid}" });

        // ---- AttributeName 生成 / 校验 ----
        string attributeName;
        bool userSpecified = !string.IsNullOrWhiteSpace(req.AttributeName);

        if (userSpecified)
        {
            attributeName = req.AttributeName!.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    attributeName, @"^[A-Za-z][A-Za-z0-9_]*$"))
                return BadRequest(new
                {
                    error = "AttributeName 必须以字母开头，只含字母、数字、下划线"
                });
            if (attributeName.Length > 200)
                return BadRequest(new { error = "AttributeName 过长（最大 200 字符）" });
        }
        else
        {
            attributeName = GenerateAttributeName();
        }

        // ---- 唯一性检查（用户指定的冲突直接 409，自动生成的碰撞重试最多 3 次）----
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var conflict = await _db.AttributeCatalog.AnyAsync(
                a => a.EntityType == req.EntityType
                  && a.AttributeName == attributeName,
                ct);

            if (!conflict) break;

            if (userSpecified)
                return Conflict(new
                {
                    error = $"属性名已存在: {req.EntityType}.{attributeName}"
                });

            // 自动生成的碰撞：重新生成
            attributeName = GenerateAttributeName();
        }

        // ---- 创建 ----
        var def = new AttributeDefinition
        {
            EntityType = req.EntityType,
            AttributeName = attributeName,
            DisplayName = req.DisplayName.Trim(),
            DataType = req.DataType,
            IsRequired = req.IsRequired,
            IsSearchable = req.IsSearchable,
            IsSortable = req.IsSortable,
            DisplayOrder = req.DisplayOrder,
            UnitId = req.UnitId,
            RefCompositeTypeId = req.RefCompositeTypeId,
            RefTableDefinitionId = req.RefTableDefinitionId,
            RefOptionSetId = req.RefOptionSetId,
            DefaultValue = req.DefaultValue,
            AllowedValues = req.AllowedValues is null
                ? null : JsonDocument.Parse(req.AllowedValues.Value.GetRawText()),
            ValidationRule = req.ValidationRule is null
                ? null : JsonDocument.Parse(req.ValidationRule.Value.GetRawText())
        };

        _db.AttributeCatalog.Add(def);
        await _db.SaveChangesAsync(ct);
        _attrCache.Invalidate(req.EntityType);

        return Ok(new { def.AttributeId, def.AttributeName });
    }

    /// <summary>
    /// 生成属性内部标识：`attr_` + 12 位随机 hex（如 attr_3f9a2b1c8d4e）。
    /// 48 bit 随机，同 EntityType 下碰撞概率可忽略；有重试保护。
    /// </summary>
    private static string GenerateAttributeName()
        => "attr_" + Guid.NewGuid().ToString("N")[..12];

    [HttpPost("composite-types")]
    public async Task<IActionResult> CreateCompositeType(
        [FromBody] CreateCompositeTypeRequest req, CancellationToken ct)
    {
        var type = new CompositeTypeDefinition
        {
            EntityType = req.EntityType,
            TypeName = req.TypeName,
            DisplayName = req.DisplayName,
            Version = 1
        };
        _db.CompositeTypes.Add(type);
        await _db.SaveChangesAsync(ct);
        return Ok(new { type.CompositeTypeId });
    }

    [HttpPost("custom-tables")]
    public async Task<IActionResult> CreateCustomTable(
        [FromBody] CreateCustomTableRequest req, CancellationToken ct)
    {
        var table = new CustomTableDefinition
        {
            EntityType = req.EntityType,
            TableName = req.TableName,
            DisplayName = req.DisplayName,
            DisplayOrder = req.DisplayOrder,
            Version = 1
        };
        _db.CustomTables.Add(table);
        await _db.SaveChangesAsync(ct);
        // ★ 清除 name 缓存中可能存在的"不存在"哨兵，使新表按名立即可见
        _customTableCache.InvalidateByName(table.EntityType, table.TableName);
        return Ok(new { table.TableDefinitionId });
    }

    /// <summary>
    /// 添加自定义表列。
    ///
    /// ★ 校验：composite 类型必须指定 refCompositeTypeId，非 composite 不允许引用。
    /// </summary>
    [HttpPost("custom-tables/{id}/columns")]
    public async Task<IActionResult> AddTableColumn(
        string id, [FromBody] CreateTableColumnRequest req, CancellationToken ct)
    {
        // 表存在性
        var tableExists = await _db.CustomTables
            .AnyAsync(t => t.TableDefinitionId == id && !t.IsDeleted, ct);
        if (!tableExists) return NotFound(new { error = $"自定义表不存在: {id}" });

        // ★ 类型与引用匹配
        if (req.DataType == EavDataTypes.Composite)
        {
            if (req.RefCompositeTypeId is not string rid)
                return BadRequest(new { error = "composite 类型必须指定 refCompositeTypeId" });

            var exists = await _db.CompositeTypes
                .AnyAsync(t => t.CompositeTypeId == rid && !t.IsDeleted, ct);
            if (!exists)
                return BadRequest(new { error = $"组合类型不存在: {rid}" });
        }
        else if (req.RefCompositeTypeId is not null)
        {
            return BadRequest(new { error = "只有 composite 类型可以引用组合类型" });
        }

        var col = new CustomTableColumn
        {
            TableDefinitionId = id,
            ColumnName = req.ColumnName,
            DisplayName = req.DisplayName,
            DataType = req.DataType,
            RefCompositeTypeId = req.RefCompositeTypeId,
            IsRequired = req.IsRequired,
            IsSearchable = req.IsSearchable,
            IsSortable = req.IsSortable,
            IsUnique = req.IsUnique,
            DisplayOrder = req.DisplayOrder,
            DefaultValue = req.DefaultValue,
            ValidationRule = req.ValidationRule is null
                ? null : JsonDocument.Parse(req.ValidationRule.Value.GetRawText()),
            AllowedValues = req.AllowedValues is null
                ? null : JsonDocument.Parse(req.AllowedValues.Value.GetRawText())
        };
        _db.CustomTableColumns.Add(col);
        await _db.SaveChangesAsync(ct);
        _customTableCache.Invalidate(id);
        return Ok(new { col.ColumnId });
    }

    /// <summary>
    /// 添加组合字段。
    ///
    /// ★ 校验：
    ///   - composite 类型必须指定 refCompositeTypeId，且不能自引用/循环引用
    ///   - 只有 decimal 类型可以绑定单位
    ///   - 只有 single_choice 类型可以引用选项集
    /// </summary>
    [HttpPost("composite-types/{id}/fields")]
    public async Task<IActionResult> AddCompositeField(
        string id, [FromBody] CreateCompositeFieldRequest req, CancellationToken ct)
    {
        // 组合类型存在性
        var typeExists = await _db.CompositeTypes
            .AnyAsync(t => t.CompositeTypeId == id && !t.IsDeleted, ct);
        if (!typeExists) return NotFound(new { error = $"组合类型不存在: {id}" });

        // 组合类型引用校验
        if (req.DataType == EavDataTypes.Composite)
        {
            if (req.RefCompositeTypeId is not string rid)
                return BadRequest(new { error = "composite 类型必须指定 refCompositeTypeId" });

            if (rid == id)
                return BadRequest(new { error = "组合类型不能自引用" });

            var exists = await _db.CompositeTypes
                .AnyAsync(t => t.CompositeTypeId == rid && !t.IsDeleted, ct);
            if (!exists)
                return BadRequest(new { error = $"组合类型不存在: {rid}" });

            // ★ 循环引用检测：从 rid 出发，看是否能回到 id
            if (await WouldCreateCycleAsync(id, rid, ct))
                return BadRequest(new { error = "会造成组合类型循环引用" });
        }
        else if (req.RefCompositeTypeId is not null)
        {
            return BadRequest(new { error = "只有 composite 类型可以引用组合类型" });
        }

        // ★ 单位只允许 decimal
        if (req.UnitId is not null)
        {
            if (req.DataType != EavDataTypes.Decimal)
                return BadRequest(new
                {
                    error = "只有 decimal 类型可以绑定单位（数据库 CHECK 约束）"
                });

            var unitExists = await _db.Units
                .AnyAsync(u => u.Id == req.UnitId.Value && !u.IsDeleted, ct);
            if (!unitExists)
                return BadRequest(new { error = $"单位不存在: {req.UnitId}" });
        }

        // 选项集引用校验
        if (req.RefOptionSetId is { } sid)
        {
            if (req.DataType != EavDataTypes.SingleChoice)
                return BadRequest(new { error = "只有 single_choice 类型可以引用选项集" });

            var exists = await _db.OptionSets.AnyAsync(s => s.OptionSetId == sid, ct);
            if (!exists) return BadRequest(new { error = $"选项集不存在: {sid}" });
        }

        var field = new CompositeFieldDefinition
        {
            CompositeTypeId = id,
            FieldName = req.FieldName,
            DisplayName = req.DisplayName,
            DataType = req.DataType,
            RefCompositeTypeId = req.RefCompositeTypeId,
            UnitId = req.UnitId,
            RefOptionSetId = req.RefOptionSetId,
            IsArray = req.IsArray,
            IsRequired = req.IsRequired,
            IsSearchable = req.IsSearchable,
            IsSortable = req.IsSortable,
            DisplayOrder = req.DisplayOrder,
            DefaultValue = req.DefaultValue,
            AllowedValues = req.AllowedValues is null
                ? null : JsonDocument.Parse(req.AllowedValues.Value.GetRawText()),
            ValidationRule = req.ValidationRule is null
                ? null : JsonDocument.Parse(req.ValidationRule.Value.GetRawText())
        };
        _db.CompositeFields.Add(field);
        await _db.SaveChangesAsync(ct);
        _compositeCache.Invalidate(id);
        return Ok(new { field.FieldId });
    }

    /// <summary>
    /// 循环引用检测：从 refTypeId 出发 BFS，若回溯到 parentTypeId 则说明
    /// 添加此字段会形成环（A→B→A 或更深），导致 CompositeTypeCache.GetType
    /// 无限递归 StackOverflow（不可捕获）。
    /// </summary>
    private async Task<bool> WouldCreateCycleAsync(
        string parentTypeId, string refTypeId, CancellationToken ct)
    {
        if (parentTypeId == refTypeId) return true;

        // 一次性加载所有组合字段的引用关系，避免 BFS 中的 N+1 查询
        var edges = await _db.CompositeFields
            .Where(f => f.RefCompositeTypeId != null && !f.IsDeleted)
            .Select(f => new { f.CompositeTypeId, Ref = f.RefCompositeTypeId! })
            .AsNoTracking()
            .ToListAsync(ct);

        var lookup = edges
            .GroupBy(e => e.CompositeTypeId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Ref).ToList());

        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(refTypeId);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == parentTypeId) return true;
            if (!visited.Add(cur)) continue;

            if (lookup.TryGetValue(cur, out var children))
            {
                foreach (var c in children) queue.Enqueue(c);
            }
        }
        return false;
    }

    // ============================================================
    // 自定义表查询与删除
    // ============================================================

    [HttpGet("custom-tables")]
    public async Task<IActionResult> ListCustomTables(
        [FromQuery] string? entityType,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = _db.CustomTables.Include(t => t.Columns).AsQueryable();
        if (!includeDeleted) query = query.Where(t => !t.IsDeleted);
        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(t => t.EntityType == entityType);

        var list = await query
            .OrderBy(t => t.EntityType).ThenBy(t => t.DisplayOrder)
            .ThenBy(t => t.TableDefinitionId)
            .AsNoTracking().ToListAsync(ct);

        return Ok(list.Select(ToCustomTableDto));
    }

    [HttpGet("custom-tables/{id}")]
    public async Task<IActionResult> GetCustomTable(string id, CancellationToken ct)
    {
        // ★ 不再检查 IsDeleted：ID 唯一标识资源，允许按 ID 查询已删除的表
        //   （恢复流程需要）。是否过滤由调用方决定。
        var table = await _db.CustomTables.Include(t => t.Columns)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TableDefinitionId == id, ct);
        if (table is null) return NotFound();
        return Ok(ToCustomTableDto(table));
    }

    [HttpPut("custom-tables/{id}")]
    public async Task<IActionResult> UpdateCustomTable(
        string id, [FromBody] UpdateCustomTableRequest req, CancellationToken ct)
    {
        var table = await _db.CustomTables.FindAsync(new object[] { id }, ct);
        if (table is null || table.IsDeleted) return NotFound();

        if (req.DisplayName is not null) table.DisplayName = req.DisplayName;
        if (req.DisplayOrder is not null) table.DisplayOrder = req.DisplayOrder.Value;
        table.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("custom-tables/{id}")]
    public async Task<IActionResult> DeleteCustomTable(string id, CancellationToken ct)
    {
        var table = await _db.CustomTables.Include(t => t.Columns)
            .FirstOrDefaultAsync(t => t.TableDefinitionId == id, ct);
        if (table is null) return NotFound();

        var referenced = await _db.AttributeCatalog
            .AnyAsync(a => a.RefTableDefinitionId == id && !a.IsDeleted, ct);
        if (referenced)
            return BadRequest(new { error = "该自定义表仍被属性引用，无法删除" });

        table.IsDeleted = true;
        table.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var c in table.Columns) c.IsDeleted = true;

        await _db.SaveChangesAsync(ct);
        _customTableCache.Invalidate(id);
        // ★ 一并清 name 缓存，否则 10 分钟内按名查询仍会命中已删表
        _customTableCache.InvalidateByName(table.EntityType, table.TableName);
        return NoContent();
    }

    [HttpPut("custom-tables/{id}/columns/{columnId}")]
    public async Task<IActionResult> UpdateTableColumn(
        string id, string columnId,
        [FromBody] UpdateTableColumnRequest req, CancellationToken ct)
    {
        var col = await _db.CustomTableColumns
            .FirstOrDefaultAsync(c => c.ColumnId == columnId && c.TableDefinitionId == id, ct);
        if (col is null || col.IsDeleted) return NotFound();

        if (req.DisplayName is not null) col.DisplayName = req.DisplayName;
        if (req.IsRequired is not null) col.IsRequired = req.IsRequired.Value;
        if (req.IsSearchable is not null) col.IsSearchable = req.IsSearchable.Value;
        if (req.IsSortable is not null) col.IsSortable = req.IsSortable.Value;
        if (req.IsUnique is not null) col.IsUnique = req.IsUnique.Value;
        if (req.DisplayOrder is not null) col.DisplayOrder = req.DisplayOrder.Value;
        if (req.DefaultValue is not null) col.DefaultValue = req.DefaultValue;
        if (req.AllowedValues is not null)
            col.AllowedValues = JsonDocument.Parse(req.AllowedValues.Value.GetRawText());
        if (req.ValidationRule is not null)
            col.ValidationRule = JsonDocument.Parse(req.ValidationRule.Value.GetRawText());

        await _db.SaveChangesAsync(ct);
        _customTableCache.Invalidate(id);
        return NoContent();
    }

    [HttpDelete("custom-tables/{id}/columns/{columnId}")]
    public async Task<IActionResult> DeleteTableColumn(
        string id, string columnId, CancellationToken ct)
    {
        var col = await _db.CustomTableColumns
            .FirstOrDefaultAsync(c => c.ColumnId == columnId && c.TableDefinitionId == id, ct);
        if (col is null) return NotFound();

        col.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        _customTableCache.Invalidate(id);
        return NoContent();
    }

    // ============================================================
    // 组合类型查询与删除
    // ============================================================

    /// <summary>
    /// 列出组合类型。
    /// ★ 新增 includeDeleted 参数：管理页可勾选"显示已删除"以提供恢复入口。
    /// </summary>
    [HttpGet("composite-types")]
    public async Task<IActionResult> ListCompositeTypes(
        [FromQuery] string? entityType,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = _db.CompositeTypes
            .Include(t => t.Fields).ThenInclude(f => f.RefOptionSet)
            .AsQueryable();

        if (!includeDeleted) query = query.Where(t => !t.IsDeleted);
        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(t => t.EntityType == entityType || t.EntityType == "Shared");

        var list = await query
            .OrderBy(t => t.EntityType).ThenBy(t => t.CompositeTypeId)
            .AsNoTracking().ToListAsync(ct);

        return Ok(list.Select(ToCompositeTypeDto));
    }

    /// <summary>
    /// 按 ID 获取组合类型详情。
    /// ★ 不再检查 IsDeleted：ID 唯一标识资源，允许按 ID 查询已删除的类型
    ///   （恢复流程需要）。是否过滤由调用方决定。
    /// </summary>
    [HttpGet("composite-types/{id}")]
    public async Task<IActionResult> GetCompositeType(string id, CancellationToken ct)
    {
        var type = await _db.CompositeTypes
            .Include(t => t.Fields).ThenInclude(f => f.RefOptionSet)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.CompositeTypeId == id, ct);
        if (type is null) return NotFound();
        return Ok(ToCompositeTypeDto(type));
    }

    [HttpPut("composite-types/{id}")]
    public async Task<IActionResult> UpdateCompositeType(
        string id, [FromBody] UpdateCompositeTypeRequest req, CancellationToken ct)
    {
        var type = await _db.CompositeTypes.FindAsync(new object[] { id }, ct);
        if (type is null || type.IsDeleted) return NotFound();

        if (req.DisplayName is not null) type.DisplayName = req.DisplayName;
        type.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("composite-types/{id}")]
    public async Task<IActionResult> DeleteCompositeType(string id, CancellationToken ct)
    {
        var type = await _db.CompositeTypes
            .Include(t => t.Fields).ThenInclude(f => f.RefOptionSet)
            .FirstOrDefaultAsync(t => t.CompositeTypeId == id && !t.IsDeleted, ct);
        if (type is null) return NotFound();

        var referenced = await _db.AttributeCatalog
            .AnyAsync(a => a.RefCompositeTypeId == id && !a.IsDeleted, ct);
        if (referenced)
            return BadRequest(new { error = "该组合类型仍被属性引用，无法删除" });

        // 也被其它组合字段引用时禁止删除
        var fieldReferenced = await _db.CompositeFields
            .AnyAsync(f => f.RefCompositeTypeId == id && !f.IsDeleted, ct);
        if (fieldReferenced)
            return BadRequest(new { error = "该组合类型仍被其它组合字段引用，无法删除" });

        type.IsDeleted = true;
        type.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var f in type.Fields) f.IsDeleted = true;

        await _db.SaveChangesAsync(ct);
        _compositeCache.Invalidate(id);
        return NoContent();
    }

    /// <summary>
    /// 更新组合字段。
    ///
    /// ★ 支持：
    ///   - 选项集引用（single_choice）
    ///   - 单位引用（decimal）：新增
    ///   - 显式清除语义：Clear* = true 优先
    /// </summary>
    [HttpPut("composite-types/{id}/fields/{fieldId}")]
    public async Task<IActionResult> UpdateCompositeField(
        string id, string fieldId,
        [FromBody] UpdateCompositeFieldRequest req, CancellationToken ct)
    {
        var field = await _db.CompositeFields
            .FirstOrDefaultAsync(f => f.FieldId == fieldId && f.CompositeTypeId == id, ct);
        if (field is null || field.IsDeleted) return NotFound();

        if (req.DisplayName is not null) field.DisplayName = req.DisplayName;
        if (req.IsRequired is not null) field.IsRequired = req.IsRequired.Value;
        if (req.IsSearchable is not null) field.IsSearchable = req.IsSearchable.Value;
        if (req.IsSortable is not null) field.IsSortable = req.IsSortable.Value;
        if (req.DisplayOrder is not null) field.DisplayOrder = req.DisplayOrder.Value;
        if (req.DefaultValue is not null) field.DefaultValue = req.DefaultValue;
        if (req.AllowedValues is not null)
            field.AllowedValues = JsonDocument.Parse(req.AllowedValues.Value.GetRawText());
        if (req.ValidationRule is not null)
            field.ValidationRule = JsonDocument.Parse(req.ValidationRule.Value.GetRawText());

        // ---- 选项集引用 ----
        if (req.ClearRefOptionSetId)
        {
            field.RefOptionSetId = null;
        }
        else if (req.RefOptionSetId is { } sid)
        {
            if (field.DataType != EavDataTypes.SingleChoice)
                return BadRequest(new { error = "只有 single_choice 类型可以引用选项集" });

            var exists = await _db.OptionSets.AnyAsync(s => s.OptionSetId == sid, ct);
            if (!exists) return BadRequest(new { error = $"选项集不存在: {sid}" });
            field.RefOptionSetId = sid;
        }

        // ★ 新增：单位引用
        if (req.ClearUnitId)
        {
            field.UnitId = null;
        }
        else if (req.UnitId is { } uid)
        {
            if (field.DataType != EavDataTypes.Decimal)
                return BadRequest(new { error = "只有 decimal 类型可以绑定单位" });

            var unitExists = await _db.Units
                .AnyAsync(u => u.Id == uid && !u.IsDeleted, ct);
            if (!unitExists) return BadRequest(new { error = $"单位不存在: {uid}" });
            field.UnitId = uid;
        }

        await _db.SaveChangesAsync(ct);
        _compositeCache.Invalidate(id);
        return NoContent();
    }

    [HttpDelete("composite-types/{id}/fields/{fieldId}")]
    public async Task<IActionResult> DeleteCompositeField(
        string id, string fieldId, CancellationToken ct)
    {
        var field = await _db.CompositeFields
            .FirstOrDefaultAsync(f => f.FieldId == fieldId && f.CompositeTypeId == id, ct);
        if (field is null) return NotFound();

        field.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        _compositeCache.Invalidate(id);
        return NoContent();
    }

    // ============================================================
    // 属性定义查询与更新
    // ============================================================

    [HttpGet("attributes")]
    public async Task<IActionResult> ListAttributes(
        [FromQuery] string? entityType,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = _db.AttributeCatalog
            .Include(a => a.Unit).Include(a => a.RefCompositeType)
            .Include(a => a.RefTableDefinition).Include(a => a.RefOptionSet)
            .AsQueryable();

        if (!includeDeleted) query = query.Where(a => !a.IsDeleted);
        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(a => a.EntityType == entityType);

        var list = await query
            .OrderBy(a => a.EntityType).ThenBy(a => a.DisplayOrder).ThenBy(a => a.AttributeId)
            .AsNoTracking().ToListAsync(ct);

        return Ok(list.Select(ToAttributeDetailDto));
    }

    [HttpGet("attributes/{id}")]
    public async Task<IActionResult> GetAttribute(string id, CancellationToken ct)
    {
        var def = await _db.AttributeCatalog
            .Include(a => a.Unit).Include(a => a.RefCompositeType)
            .Include(a => a.RefTableDefinition).Include(a => a.RefOptionSet)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AttributeId == id, ct);
        if (def is null) return NotFound();
        return Ok(ToAttributeDetailDto(def));
    }

    /// <summary>
    /// 更新属性定义。
    ///
    /// ★ int / decimal 均可绑定单位。
    /// 不可修改：EntityType、AttributeName、DataType。
    /// </summary>
    [HttpPut("attributes/{id}")]
    public async Task<IActionResult> UpdateAttribute(
        string id, [FromBody] UpdateAttributeRequest req, CancellationToken ct)
    {
        var def = await _db.AttributeCatalog.FindAsync(new object[] { id }, ct);
        if (def is null || def.IsDeleted) return NotFound();

        if (req.DisplayName is not null) def.DisplayName = req.DisplayName;
        if (req.IsRequired is not null) def.IsRequired = req.IsRequired.Value;
        if (req.IsSearchable is not null) def.IsSearchable = req.IsSearchable.Value;
        if (req.IsSortable is not null) def.IsSortable = req.IsSortable.Value;
        if (req.DisplayOrder is not null) def.DisplayOrder = req.DisplayOrder.Value;
        if (req.DefaultValue is not null) def.DefaultValue = req.DefaultValue;

        if (req.AllowedValues is not null)
            def.AllowedValues = JsonDocument.Parse(req.AllowedValues.Value.GetRawText());
        if (req.ValidationRule is not null)
            def.ValidationRule = JsonDocument.Parse(req.ValidationRule.Value.GetRawText());

        // ---- 单位引用 ----
        if (req.ClearUnitId)
        {
            def.UnitId = null;
        }
        else if (req.UnitId is not null)
        {
            var exists = await _db.Units.AnyAsync(
                u => u.Id == req.UnitId.Value && !u.IsDeleted, ct);
            if (!exists) return BadRequest(new { error = $"单位不存在: {req.UnitId}" });
            def.UnitId = req.UnitId;
        }

        // ---- 组合类型引用 ----
        if (req.ClearRefCompositeTypeId)
        {
            def.RefCompositeTypeId = null;
        }
        else if (req.RefCompositeTypeId is not null)
        {
            if (def.DataType != EavDataTypes.Composite)
                return BadRequest(new { error = "只有 composite 类型可以引用组合类型" });

            var exists = await _db.CompositeTypes.AnyAsync(
                t => t.CompositeTypeId == req.RefCompositeTypeId && !t.IsDeleted, ct);
            if (!exists) return BadRequest(new { error = $"组合类型不存在: {req.RefCompositeTypeId}" });
            def.RefCompositeTypeId = req.RefCompositeTypeId;
        }

        // ---- 自定义表引用 ----
        if (req.ClearRefTableDefinitionId)
        {
            def.RefTableDefinitionId = null;
        }
        else if (req.RefTableDefinitionId is not null)
        {
            if (def.DataType != EavDataTypes.Table)
                return BadRequest(new { error = "只有 table 类型可以引用自定义表" });

            var exists = await _db.CustomTables.AnyAsync(
                t => t.TableDefinitionId == req.RefTableDefinitionId && !t.IsDeleted, ct);
            if (!exists) return BadRequest(new { error = $"自定义表不存在: {req.RefTableDefinitionId}" });
            def.RefTableDefinitionId = req.RefTableDefinitionId;
        }

        // ---- 选项集引用 ----
        if (req.ClearRefOptionSetId)
        {
            def.RefOptionSetId = null;
        }
        else if (req.RefOptionSetId is not null)
        {
            if (def.DataType != EavDataTypes.SingleChoice)
                return BadRequest(new { error = "只有 single_choice 类型可以引用选项集" });

            var exists = await _db.OptionSets.AnyAsync(
                s => s.OptionSetId == req.RefOptionSetId, ct);
            if (!exists) return BadRequest(new { error = $"选项集不存在: {req.RefOptionSetId}" });
            def.RefOptionSetId = req.RefOptionSetId;
        }

        def.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _attrCache.Invalidate(def.EntityType);
        return NoContent();
    }

    [HttpDelete("attributes/{id}")]
    public async Task<IActionResult> DeleteAttribute(string id, CancellationToken ct)
    {
        var def = await _db.AttributeCatalog.FindAsync(new object[] { id }, ct);
        if (def is null) return NotFound();

        def.IsDeleted = true;
        def.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _attrCache.Invalidate(def.EntityType);
        return NoContent();
    }

    // ============================================================
    // DTO 映射
    // ============================================================

    private static AttributeDetailDto ToAttributeDetailDto(AttributeDefinition a) => new(
        a.AttributeId, a.EntityType, a.AttributeName, a.DisplayName, a.DataType,
        a.IsRequired, a.IsSearchable, a.IsSortable, a.IsDeleted,
        a.Version, a.DisplayOrder, a.DefaultValue, a.CreatedAt, a.UpdatedAt,
        a.AllowedValues != null ? a.AllowedValues.RootElement.Clone() : (JsonElement?)null,
        a.ValidationRule != null ? a.ValidationRule.RootElement.Clone() : (JsonElement?)null,
        a.UnitId, a.Unit?.Name, a.Unit?.Symbol, a.Unit?.Category,
        a.RefCompositeTypeId, a.RefCompositeType?.TypeName, a.RefCompositeType?.DisplayName,
        a.RefTableDefinitionId, a.RefTableDefinition?.TableName, a.RefTableDefinition?.DisplayName,
        a.RefOptionSetId, a.RefOptionSet?.SetName, a.RefOptionSet?.DisplayName);

    private static CustomTableDetailDto ToCustomTableDto(CustomTableDefinition t) => new(
        t.TableDefinitionId, t.EntityType, t.TableName, t.DisplayName,
        t.Version, t.DisplayOrder,
        t.Columns
            // ★ 不按 IsDeleted 过滤，让前端展示"已删除"状态并提供恢复入口
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new CustomTableColumnDto(
                c.ColumnId, c.ColumnName, c.DisplayName, c.DataType, c.RefCompositeTypeId,
                c.IsRequired, c.IsSearchable, c.IsSortable, c.IsUnique, c.DisplayOrder,
                c.DefaultValue,
                c.AllowedValues != null ? c.AllowedValues.RootElement.Clone() : (JsonElement?)null,
                c.ValidationRule != null ? c.ValidationRule.RootElement.Clone() : (JsonElement?)null,
                c.IsDeleted))                    // ★ 透出列的删除状态
            .ToList(),
        t.IsDeleted);                            // ★ 透出表的删除状态

    private static CompositeTypeDetailDto ToCompositeTypeDto(CompositeTypeDefinition t) => new(
        t.CompositeTypeId, t.EntityType, t.TypeName, t.DisplayName, t.Version,
        t.Fields
            // ★ 不按 IsDeleted 过滤，让前端展示"已删除"状态 + 提供恢复入口
            .OrderBy(f => f.DisplayOrder)
            .Select(f => new CompositeFieldDetailDto(
                f.FieldId, f.FieldName, f.DisplayName, f.DataType, f.RefCompositeTypeId,
                f.IsArray, f.IsRequired, f.IsSearchable, f.IsSortable, f.DisplayOrder,
                f.DefaultValue,
                f.AllowedValues != null ? f.AllowedValues.RootElement.Clone() : (JsonElement?)null,
                f.ValidationRule != null ? f.ValidationRule.RootElement.Clone() : (JsonElement?)null,
                f.UnitId,
                f.RefOptionSetId,
                f.RefOptionSet?.SetName,
                f.RefOptionSet?.DisplayName,
                f.IsDeleted))                    // ★ 透出字段删除状态
            .ToList(),
        t.IsDeleted);                            // ★ 透出类型删除状态

    // ---------- 软删除恢复（undelete） ----------

    /// <summary>
    /// ★ 恢复被软删除的属性。
    ///
    /// 唯一约束（entity_type, attribute_name）不区分 IsDeleted：
    /// 若已存在同名的活动属性，恢复会失败 → 返回 409。
    /// </summary>
    [HttpPost("attributes/{id}/undelete")]
    public async Task<IActionResult> UndeleteAttribute(string id, CancellationToken ct)
    {
        var def = await _db.AttributeCatalog.FindAsync(new object[] { id }, ct);
        if (def is null) return NotFound();
        if (!def.IsDeleted) return NoContent();   // 幂等

        var conflict = await _db.AttributeCatalog.AnyAsync(
            a => a.AttributeId != id
              && a.EntityType == def.EntityType
              && a.AttributeName == def.AttributeName
              && !a.IsDeleted, ct);

        if (conflict)
            return Conflict(new
            {
                error = $"同名的活动属性已存在（{def.EntityType}.{def.AttributeName}），" +
                        "请先删除或改名后再恢复"
            });

        def.IsDeleted = false;
        def.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _attrCache.Invalidate(def.EntityType);
        return NoContent();
    }

    /// <summary>
    /// ★ 恢复被软删除的组合类型（同时恢复其字段）。
    /// 唯一约束（entity_type, type_name, version）不区分 IsDeleted。
    /// </summary>
    [HttpPost("composite-types/{id}/undelete")]
    public async Task<IActionResult> UndeleteCompositeType(string id, CancellationToken ct)
    {
        var type = await _db.CompositeTypes
            .Include(t => t.Fields)
            .FirstOrDefaultAsync(t => t.CompositeTypeId == id, ct);
        if (type is null) return NotFound();
        if (!type.IsDeleted) return NoContent();   // 幂等

        var conflict = await _db.CompositeTypes.AnyAsync(
            t => t.CompositeTypeId != id
              && t.EntityType == type.EntityType
              && t.TypeName == type.TypeName
              && t.Version == type.Version
              && !t.IsDeleted, ct);

        if (conflict)
            return Conflict(new
            {
                error = $"同名的活动组合类型已存在" +
                        $"（{type.EntityType}/{type.TypeName}/v{type.Version}）"
            });

        type.IsDeleted = false;
        type.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var f in type.Fields) f.IsDeleted = false;

        await _db.SaveChangesAsync(ct);
        _compositeCache.Invalidate(id);
        return NoContent();
    }

    /// <summary>
    /// ★ 恢复被软删除的组合字段。
    /// 唯一约束（composite_type_id, field_name）不区分 IsDeleted。
    /// </summary>
    [HttpPost("composite-types/{id}/fields/{fieldId}/undelete")]
    public async Task<IActionResult> UndeleteCompositeField(
        string id, string fieldId, CancellationToken ct)
    {
        var field = await _db.CompositeFields
            .FirstOrDefaultAsync(f => f.FieldId == fieldId && f.CompositeTypeId == id, ct);
        if (field is null) return NotFound();
        if (!field.IsDeleted) return NoContent();

        var conflict = await _db.CompositeFields.AnyAsync(
            f => f.FieldId != fieldId
              && f.CompositeTypeId == id
              && f.FieldName == field.FieldName
              && !f.IsDeleted, ct);

        if (conflict)
            return Conflict(new
            {
                error = $"同名的活动字段已存在（{field.FieldName}）"
            });

        field.IsDeleted = false;
        await _db.SaveChangesAsync(ct);
        _compositeCache.Invalidate(id);
        return NoContent();
    }

    /// <summary>
    /// ★ 恢复被软删除的自定义表（同时恢复其列）。
    /// 唯一约束（entity_type, table_name, version）不区分 IsDeleted。
    /// </summary>
    [HttpPost("custom-tables/{id}/undelete")]
    public async Task<IActionResult> UndeleteCustomTable(string id, CancellationToken ct)
    {
        var table = await _db.CustomTables
            .Include(t => t.Columns)
            .FirstOrDefaultAsync(t => t.TableDefinitionId == id, ct);
        if (table is null) return NotFound();
        if (!table.IsDeleted) return NoContent();

        var conflict = await _db.CustomTables.AnyAsync(
            t => t.TableDefinitionId != id
              && t.EntityType == table.EntityType
              && t.TableName == table.TableName
              && t.Version == table.Version
              && !t.IsDeleted, ct);

        if (conflict)
            return Conflict(new
            {
                error = $"同名的活动自定义表已存在" +
                        $"（{table.EntityType}/{table.TableName}/v{table.Version}）"
            });

        table.IsDeleted = false;
        table.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var c in table.Columns) c.IsDeleted = false;

        await _db.SaveChangesAsync(ct);

        // ★ 缓存一致性：实体 + name→id 两级都要清
        _customTableCache.Invalidate(id);
        _customTableCache.InvalidateByName(table.EntityType, table.TableName);

        return NoContent();
    }

    /// <summary>
    /// ★ 恢复被软删除的自定义表列。
    /// 唯一约束（table_definition_id, column_name）不区分 IsDeleted。
    /// </summary>
    [HttpPost("custom-tables/{id}/columns/{columnId}/undelete")]
    public async Task<IActionResult> UndeleteTableColumn(
        string id, string columnId, CancellationToken ct)
    {
        var col = await _db.CustomTableColumns
            .FirstOrDefaultAsync(c => c.ColumnId == columnId
                                   && c.TableDefinitionId == id, ct);
        if (col is null) return NotFound();
        if (!col.IsDeleted) return NoContent();

        var conflict = await _db.CustomTableColumns.AnyAsync(
            c => c.ColumnId != columnId
              && c.TableDefinitionId == id
              && c.ColumnName == col.ColumnName
              && !c.IsDeleted, ct);

        if (conflict)
            return Conflict(new
            {
                error = $"同名的活动列已存在（{col.ColumnName}）"
            });

        col.IsDeleted = false;
        await _db.SaveChangesAsync(ct);
        _customTableCache.Invalidate(id);
        return NoContent();
    }
}

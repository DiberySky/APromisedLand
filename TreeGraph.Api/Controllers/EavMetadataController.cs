using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Api.Services;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Controllers;

[ApiController]
[Route("api/eav/metadata")]
public class EavMetadataController : ControllerBase
{
    private readonly EavDbContext _db;
    private readonly IAttributeCache _attrCache;
    private readonly ICompositeTypeCache _compositeCache;
    private readonly ICustomTableCache _customTableCache;

    public EavMetadataController(
        EavDbContext db, IAttributeCache attrCache, ICompositeTypeCache compositeCache,
        ICustomTableCache customTableCache)
    {
        _db = db;
        _attrCache = attrCache;
        _compositeCache = compositeCache;
        _customTableCache = customTableCache;
    }

    /// <summary>创建属性定义（★ 修复 P1-2：全量引用校验）</summary>
    [HttpPost("attributes")]
    public async Task<IActionResult> CreateAttribute(
        [FromBody] CreateAttributeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.EntityType)
            || string.IsNullOrWhiteSpace(req.AttributeName))
            return BadRequest(new { error = "EntityType 与 AttributeName 必填" });

        if (!EavDataTypes.All.Contains(req.DataType))
            return BadRequest(new { error = $"未知的 dataType: {req.DataType}" });

        // 引用互斥
        var refCount = new[] {
            req.RefCompositeTypeId, req.RefTableDefinitionId, req.RefOptionSetId
        }.Count(x => x is not null);
        if (refCount > 1)
            return BadRequest(new { error = "组合类型 / 自定义表 / 选项集引用互斥" });

        // 类型与引用匹配
        if (req.DataType == EavDataTypes.Composite && req.RefCompositeTypeId is null)
            return BadRequest(new { error = "composite 必须指定 refCompositeTypeId" });
        if (req.DataType == EavDataTypes.Table && req.RefTableDefinitionId is null)
            return BadRequest(new { error = "table 必须指定 refTableDefinitionId" });
        if (req.DataType == EavDataTypes.SingleChoice && req.RefOptionSetId is null)
            return BadRequest(new { error = "single_choice 必须指定 refOptionSetId" });
        if (req.DataType is not (EavDataTypes.Composite or EavDataTypes.Table
            or EavDataTypes.SingleChoice) && refCount > 0)
            return BadRequest(new { error = "当前 dataType 不支持引用" });

        // 单位只允许 int/decimal
        if (req.UnitId is not null
            && req.DataType is not (EavDataTypes.Int or EavDataTypes.Decimal))
            return BadRequest(new { error = "只有 int/decimal 可以绑定单位" });

        // 引用存在性
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

        var def = new AttributeDefinition
        {
            EntityType = req.EntityType,
            AttributeName = req.AttributeName,
            DisplayName = req.DisplayName,
            DataType = req.DataType,
            IsRequired = req.IsRequired,
            IsSearchable = req.IsSearchable,
            IsSortable = req.IsSortable,
            IsMultiValue = req.IsMultiValue,
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

        return Ok(new { def.AttributeId });
    }

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
        return Ok(new { table.TableDefinitionId });
    }

    [HttpPost("custom-tables/{id:long}/columns")]
    public async Task<IActionResult> AddTableColumn(
        long id, [FromBody] CreateTableColumnRequest req, CancellationToken ct)
    {
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

    [HttpPost("composite-types/{id:long}/fields")]
    public async Task<IActionResult> AddCompositeField(
        long id, [FromBody] CreateCompositeFieldRequest req, CancellationToken ct)
    {
        var field = new CompositeFieldDefinition
        {
            CompositeTypeId = id,
            FieldName = req.FieldName,
            DisplayName = req.DisplayName,
            DataType = req.DataType,
            RefCompositeTypeId = req.RefCompositeTypeId,
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

    // ============================================================
    // 自定义表查询与删除
    // ============================================================

    [HttpGet("custom-tables")]
    public async Task<IActionResult> ListCustomTables(
        [FromQuery] string? entityType, CancellationToken ct)
    {
        var query = _db.CustomTables.Include(t => t.Columns).Where(t => !t.IsDeleted);
        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(t => t.EntityType == entityType);

        var list = await query
            .OrderBy(t => t.EntityType).ThenBy(t => t.DisplayOrder)
            .ThenBy(t => t.TableDefinitionId)
            .AsNoTracking().ToListAsync(ct);

        return Ok(list.Select(ToCustomTableDto));
    }

    [HttpGet("custom-tables/{id:long}")]
    public async Task<IActionResult> GetCustomTable(long id, CancellationToken ct)
    {
        var table = await _db.CustomTables.Include(t => t.Columns)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TableDefinitionId == id, ct);
        if (table is null || table.IsDeleted) return NotFound();
        return Ok(ToCustomTableDto(table));
    }

    [HttpPut("custom-tables/{id:long}")]
    public async Task<IActionResult> UpdateCustomTable(
        long id, [FromBody] UpdateCustomTableRequest req, CancellationToken ct)
    {
        var table = await _db.CustomTables.FindAsync(new object[] { id }, ct);
        if (table is null || table.IsDeleted) return NotFound();

        if (req.DisplayName is not null) table.DisplayName = req.DisplayName;
        if (req.DisplayOrder is not null) table.DisplayOrder = req.DisplayOrder.Value;
        table.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("custom-tables/{id:long}")]
    public async Task<IActionResult> DeleteCustomTable(long id, CancellationToken ct)
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
        return NoContent();
    }

    [HttpPut("custom-tables/{id:long}/columns/{columnId:long}")]
    public async Task<IActionResult> UpdateTableColumn(
        long id, long columnId,
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

    [HttpDelete("custom-tables/{id:long}/columns/{columnId:long}")]
    public async Task<IActionResult> DeleteTableColumn(
        long id, long columnId, CancellationToken ct)
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

    [HttpGet("composite-types")]
    public async Task<IActionResult> ListCompositeTypes(
        [FromQuery] string? entityType, CancellationToken ct)
    {
        var query = _db.CompositeTypes.Include(t => t.Fields).Where(t => !t.IsDeleted);
        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(t => t.EntityType == entityType || t.EntityType == "Shared");

        var list = await query
            .OrderBy(t => t.EntityType).ThenBy(t => t.CompositeTypeId)
            .AsNoTracking().ToListAsync(ct);

        return Ok(list.Select(ToCompositeTypeDto));
    }

    [HttpGet("composite-types/{id:long}")]
    public async Task<IActionResult> GetCompositeType(long id, CancellationToken ct)
    {
        var type = await _db.CompositeTypes.Include(t => t.Fields)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.CompositeTypeId == id, ct);
        if (type is null || type.IsDeleted) return NotFound();
        return Ok(ToCompositeTypeDto(type));
    }

    [HttpPut("composite-types/{id:long}")]
    public async Task<IActionResult> UpdateCompositeType(
        long id, [FromBody] UpdateCompositeTypeRequest req, CancellationToken ct)
    {
        var type = await _db.CompositeTypes.FindAsync(new object[] { id }, ct);
        if (type is null || type.IsDeleted) return NotFound();

        if (req.DisplayName is not null) type.DisplayName = req.DisplayName;
        type.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("composite-types/{id:long}")]
    public async Task<IActionResult> DeleteCompositeType(long id, CancellationToken ct)
    {
        var type = await _db.CompositeTypes.Include(t => t.Fields)
            .FirstOrDefaultAsync(t => t.CompositeTypeId == id, ct);
        if (type is null) return NotFound();

        var referenced = await _db.AttributeCatalog
            .AnyAsync(a => a.RefCompositeTypeId == id && !a.IsDeleted, ct);
        if (referenced)
            return BadRequest(new { error = "该组合类型仍被属性引用，无法删除" });

        type.IsDeleted = true;
        type.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var f in type.Fields) f.IsDeleted = true;

        await _db.SaveChangesAsync(ct);
        _compositeCache.Invalidate(id);
        return NoContent();
    }

    [HttpPut("composite-types/{id:long}/fields/{fieldId:long}")]
    public async Task<IActionResult> UpdateCompositeField(
        long id, long fieldId,
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

        await _db.SaveChangesAsync(ct);
        _compositeCache.Invalidate(id);
        return NoContent();
    }

    [HttpDelete("composite-types/{id:long}/fields/{fieldId:long}")]
    public async Task<IActionResult> DeleteCompositeField(
        long id, long fieldId, CancellationToken ct)
    {
        var field = await _db.CompositeFields
            .FirstOrDefaultAsync(f => f.FieldId == fieldId && f.CompositeTypeId == id, ct);
        if (field is null) return NotFound();

        field.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        _compositeCache.Invalidate(id);
        return NoContent();
    }

    // ---------- DTO 映射 ----------

    private static AttributeDetailDto ToAttributeDetailDto(AttributeDefinition a) => new(
        a.AttributeId, a.EntityType, a.AttributeName, a.DisplayName, a.DataType,
        a.IsRequired, a.IsSearchable, a.IsSortable, a.IsMultiValue, a.IsDeleted,
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
        t.Columns.Where(c => !c.IsDeleted).OrderBy(c => c.DisplayOrder)
            .Select(c => new CustomTableColumnDto(
                c.ColumnId, c.ColumnName, c.DisplayName, c.DataType, c.RefCompositeTypeId,
                c.IsRequired, c.IsSearchable, c.IsSortable, c.IsUnique, c.DisplayOrder,
                c.DefaultValue,
                c.AllowedValues != null ? c.AllowedValues.RootElement.Clone() : (JsonElement?)null,
                c.ValidationRule != null ? c.ValidationRule.RootElement.Clone() : (JsonElement?)null))
            .ToList());

    private static CompositeTypeDetailDto ToCompositeTypeDto(CompositeTypeDefinition t) => new(
        t.CompositeTypeId, t.EntityType, t.TypeName, t.DisplayName, t.Version,
        t.Fields.Where(f => !f.IsDeleted).OrderBy(f => f.DisplayOrder)
            .Select(f => new CompositeFieldDetailDto(
                f.FieldId, f.FieldName, f.DisplayName, f.DataType, f.RefCompositeTypeId,
                f.IsArray, f.IsRequired, f.IsSearchable, f.IsSortable, f.DisplayOrder,
                f.DefaultValue,
                f.AllowedValues != null ? f.AllowedValues.RootElement.Clone() : (JsonElement?)null,
                f.ValidationRule != null ? f.ValidationRule.RootElement.Clone() : (JsonElement?)null))
            .ToList());

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

    [HttpGet("attributes/{id:long}")]
    public async Task<IActionResult> GetAttribute(long id, CancellationToken ct)
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
    /// 更新属性定义（★ 修复 P1-3：支持显式清除引用）。
    /// 不可修改：EntityType、AttributeName、DataType。
    /// </summary>
    [HttpPut("attributes/{id:long}")]
    public async Task<IActionResult> UpdateAttribute(
        long id, [FromBody] UpdateAttributeRequest req, CancellationToken ct)
    {
        var def = await _db.AttributeCatalog.FindAsync(new object[] { id }, ct);
        if (def is null || def.IsDeleted) return NotFound();

        if (req.DisplayName is not null) def.DisplayName = req.DisplayName;
        if (req.IsRequired is not null) def.IsRequired = req.IsRequired.Value;
        if (req.IsSearchable is not null) def.IsSearchable = req.IsSearchable.Value;
        if (req.IsSortable is not null) def.IsSortable = req.IsSortable.Value;
        if (req.IsMultiValue is not null) def.IsMultiValue = req.IsMultiValue.Value;
        if (req.DisplayOrder is not null) def.DisplayOrder = req.DisplayOrder.Value;
        if (req.DefaultValue is not null) def.DefaultValue = req.DefaultValue;

        if (req.AllowedValues is not null)
            def.AllowedValues = JsonDocument.Parse(req.AllowedValues.Value.GetRawText());
        if (req.ValidationRule is not null)
            def.ValidationRule = JsonDocument.Parse(req.ValidationRule.Value.GetRawText());

        // 单位引用：清除 / 设置
        if (req.ClearUnitId)
        {
            def.UnitId = null;
        }
        else if (req.UnitId is not null)
        {
            if (def.DataType is not (EavDataTypes.Int or EavDataTypes.Decimal))
                return BadRequest(new { error = "只有 int/decimal 类型可以绑定单位" });

            var exists = await _db.Units.AnyAsync(
                u => u.Id == req.UnitId.Value && !u.IsDeleted, ct);
            if (!exists) return BadRequest(new { error = $"单位不存在: {req.UnitId}" });
            def.UnitId = req.UnitId;
        }

        // 组合类型引用
        if (req.ClearRefCompositeTypeId)
        {
            def.RefCompositeTypeId = null;
        }
        else if (req.RefCompositeTypeId is not null)
        {
            if (def.DataType != EavDataTypes.Composite)
                return BadRequest(new { error = "只有 composite 类型可以引用组合类型" });

            var exists = await _db.CompositeTypes.AnyAsync(
                t => t.CompositeTypeId == req.RefCompositeTypeId.Value && !t.IsDeleted, ct);
            if (!exists) return BadRequest(new { error = $"组合类型不存在: {req.RefCompositeTypeId}" });
            def.RefCompositeTypeId = req.RefCompositeTypeId;
        }

        // 自定义表引用
        if (req.ClearRefTableDefinitionId)
        {
            def.RefTableDefinitionId = null;
        }
        else if (req.RefTableDefinitionId is not null)
        {
            if (def.DataType != EavDataTypes.Table)
                return BadRequest(new { error = "只有 table 类型可以引用自定义表" });

            var exists = await _db.CustomTables.AnyAsync(
                t => t.TableDefinitionId == req.RefTableDefinitionId.Value && !t.IsDeleted, ct);
            if (!exists) return BadRequest(new { error = $"自定义表不存在: {req.RefTableDefinitionId}" });
            def.RefTableDefinitionId = req.RefTableDefinitionId;
        }

        // 选项集引用
        if (req.ClearRefOptionSetId)
        {
            def.RefOptionSetId = null;
        }
        else if (req.RefOptionSetId is not null)
        {
            if (def.DataType != EavDataTypes.SingleChoice)
                return BadRequest(new { error = "只有 single_choice 类型可以引用选项集" });

            var exists = await _db.OptionSets.AnyAsync(
                s => s.OptionSetId == req.RefOptionSetId.Value, ct);
            if (!exists) return BadRequest(new { error = $"选项集不存在: {req.RefOptionSetId}" });
            def.RefOptionSetId = req.RefOptionSetId;
        }

        def.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _attrCache.Invalidate(def.EntityType);
        return NoContent();
    }

    [HttpDelete("attributes/{id:long}")]
    public async Task<IActionResult> DeleteAttribute(long id, CancellationToken ct)
    {
        var def = await _db.AttributeCatalog.FindAsync(new object[] { id }, ct);
        if (def is null) return NotFound();

        def.IsDeleted = true;
        def.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _attrCache.Invalidate(def.EntityType);
        return NoContent();
    }
}

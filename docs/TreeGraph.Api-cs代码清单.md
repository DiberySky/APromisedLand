# TreeGraph.Api C# 代码清单

- 生成时间：2026-10-03 05:42:16
- 文件总数：41
- 排除：bin/、obj/
- 项目状态：ID 到 GUID String 重构完成（主键 HasSentinel 空串 + DB DEFAULT gen_random_uuid()::text）；GUID String 形态已包含在 Initial 迁移中，迁移链：20261003070415_Initial → 20261003230729_AddStringTreeNodes

## 文件 1/41 TreeGraph.Api/Controllers/CustomTableDataController.cs

```csharp
using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.Services;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Controllers;

[ApiController]
[Route("api/eav/{entityType}/entities/{entityId}/tables")]
public class CustomTableDataController : ControllerBase
{
    private readonly CustomTableReadService _read;
    private readonly CustomTableWriteService _write;
    private readonly IAttributeCache _attrCache;

    public CustomTableDataController(
        CustomTableReadService read, CustomTableWriteService write,
        IAttributeCache attrCache)
    {
        _read = read;
        _write = write;
        _attrCache = attrCache;
    }

    [HttpGet("{tableName}")]
    public async Task<IActionResult> Load(
        string entityType, string entityId, string tableName,
        CancellationToken ct)
    {
        var def = _attrCache.GetDefinition(entityType, tableName);
        if (def?.RefTableDefinitionId is null) return NotFound();

        var result = await _read.LoadAsync(
            entityId, entityType,
            def.AttributeId, def.RefTableDefinitionId, ct);
        return Ok(result);
    }

    [HttpPut("{tableName}")]
    public async Task<IActionResult> Replace(
        string entityType, string entityId, string tableName,
        [FromBody] CustomTableValue value, CancellationToken ct)
    {
        var def = _attrCache.GetDefinition(entityType, tableName);
        if (def?.RefTableDefinitionId is null) return NotFound();

        try
        {
            await _write.ReplaceAsync(
                entityId, entityType,
                def.AttributeId, def.RefTableDefinitionId, value, ct);
            return NoContent();
        }
        catch (EavValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpPut("{tableName}/rows")]
    public async Task<IActionResult> UpsertRow(
        string entityType, string entityId, string tableName,
        [FromBody] CustomTableRowValue rowValue, CancellationToken ct)
    {
        var def = _attrCache.GetDefinition(entityType, tableName);
        if (def?.RefTableDefinitionId is null) return NotFound();

        try
        {
            await _write.UpsertRowAsync(
                entityId, entityType,
                def.AttributeId, def.RefTableDefinitionId,
                rowValue, ct);
            return NoContent();
        }
        catch (EavValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>删除单行（★ 修复 P0-3：带归属校验；rowId 是 GUID 字符串）</summary>
    [HttpDelete("{tableName}/rows/{rowId}")]
    public async Task<IActionResult> DeleteRow(
        string entityType, string entityId, string tableName,
        string rowId, CancellationToken ct)
    {
        var def = _attrCache.GetDefinition(entityType, tableName);
        if (def?.RefTableDefinitionId is null) return NotFound();

        await _write.DeleteRowAsync(
            rowId, entityId, entityType, def.AttributeId, ct);
        return NoContent();
    }
}
```

## 文件 2/41 TreeGraph.Api/Controllers/EavController.cs

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TreeGraph.Api.Entities;
using TreeGraph.Api.Services;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Controllers;

[ApiController]
[Route("api/eav/{entityType}")]
public class EavController : ControllerBase
{
    private readonly EavWriteService _write;
    private readonly EavReadService _read;
    private readonly EavQueryService _query;
    private readonly CustomTableQueryService _tableQuery;
    private readonly IAttributeCache _attrCache;
    private readonly ICompositeTypeCache _compositeCache;
    private readonly IUnitCache _unitCache;
    private readonly IOptionSetCache _optionSetCache;
    private readonly JsonSerializerOptions _jsonOptions;

    public EavController(
        EavWriteService write, EavReadService read, EavQueryService query,
        CustomTableQueryService tableQuery,
        IAttributeCache attrCache, ICompositeTypeCache compositeCache,
        IUnitCache unitCache, IOptionSetCache optionSetCache,
        IOptions<JsonOptions> jsonOptions)
    {
        _write = write;
        _read = read;
        _query = query;
        _tableQuery = tableQuery;
        _attrCache = attrCache;
        _compositeCache = compositeCache;
        _unitCache = unitCache;
        _optionSetCache = optionSetCache;
        _jsonOptions = jsonOptions.Value.JsonSerializerOptions;
    }

    /// <summary>
    /// ★ #3：把 DynamicEntity 映射为 DTO 时带上 UpdatedAt。
    /// UpdatedAt 由 EavReadService.LoadAsync 查询时一并填充到 DynamicEntity。
    /// </summary>
    private DynamicEntityDto ToDto(DynamicEntity entity)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var (k, v) in entity.Properties)
        {
            dict[k] = v is null
                ? JsonDocument.Parse("null").RootElement.Clone()
                : JsonSerializer.SerializeToElement(v, v.GetType(), _jsonOptions);
        }
        return new DynamicEntityDto(
            entity.EntityId, entity.EntityType, dict, entity.UpdatedAt);
    }

    [HttpGet("schema")]
    public IActionResult GetSchema(string entityType)
    {
        var defs = _attrCache.GetDefinitions(entityType);
        var schema = defs.Select(ToSchemaDto).ToList();
        return Ok(new { EntityType = entityType, Attributes = schema });
    }

    [HttpGet("entities/{id}")]
    public async Task<IActionResult> Get(
        string id, string entityType,
        [FromQuery] string? unit,
        CancellationToken ct)
    {
        var originalUnits = unit == "original";
        var entity = await _read.LoadAsync(id, entityType, originalUnits, ct);
        return Ok(ToDto(entity));
    }

    /// <summary>
    /// ★ #3：PUT 支持乐观锁。
    ///
    /// 客户端从 GET 拿到的 UpdatedAt 通过 header 传回：
    ///   X-Expected-Updated-At: 2026-10-01T10:30:00.0000000+00:00
    ///
    /// 服务端比对 DB 中最新 UpdatedAt，不符则 409 Conflict 并返回最新值。
    /// header 缺失时跳过冲突检测（向后兼容）。
    /// </summary>
    [HttpPut("entities/{id}")]
    public async Task<IActionResult> Put(
        string id, string entityType,
        [FromBody] Dictionary<string, JsonElement> values,
        CancellationToken ct)
    {
        // ★ 修复：用 InvariantCulture + RoundtripKind 解析 header，
        //   避免 CurrentCulture（如 zh-CN / de-DE）对 ISO 8601 的差异化解释。
        DateTimeOffset? expectedUpdatedAt = null;
        var headerValue = Request.Headers["X-Expected-Updated-At"].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerValue)
            && DateTimeOffset.TryParse(
                headerValue,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            expectedUpdatedAt = parsed;
        }

        try
        {
            var typedValues = ConvertJsonValues(entityType, values);
            await _write.SaveAsync(id, entityType, typedValues,
                User.Identity?.Name ?? "system",
                HttpContext.TraceIdentifier,
                ct,
                expectedUpdatedAt);
            return NoContent();
        }
        catch (EavConcurrencyException ex)
        {
            return Conflict(new
            {
                error = "并发冲突：实体已被其他用户修改，请刷新后重试",
                currentUpdatedAt = ex.CurrentUpdatedAt,
                expectedUpdatedAt = ex.ExpectedUpdatedAt
            });
        }
        catch (EavValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        // ★ P2-3：table 类型属性误传时，ConvertJsonValues 抛 ArgumentException
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("entities/query")]
    public async Task<IActionResult> Query(
        string entityType, [FromBody] EavQueryRequest request,
        CancellationToken ct)
    {
        request.EntityType = entityType;
        try
        {
            var result = await _query.QueryAsync(request, ct);
            var dto = new PagedResult<DynamicEntityDto>
            {
                Items = result.Items.Select(ToDto).ToList(),
                Total = result.Total,
                Page = result.Page,
                PageSize = result.PageSize
            };
            return Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("entities/query-by-table")]
    public async Task<IActionResult> QueryByTable(
        string entityType, [FromBody] QueryByTableRequest req,
        CancellationToken ct)
    {
        var def = _attrCache.GetDefinition(entityType, req.AttributeName);
        if (def?.RefTableDefinitionId is null) return NotFound();

        var ids = await _tableQuery.FindByMultipleRowConditionsAsync(
            entityType, def.AttributeId, req.RowConditions, ct);

        return Ok(new { EntityIds = ids });
    }

    [HttpGet("entities/{id}/history")]
    public async Task<IActionResult> History(
        string id, string entityType,
        [FromQuery] DateTimeOffset? from,
        CancellationToken ct)
    {
        var history = await _read.GetHistoryAsync(id, entityType, from, ct);

        // ★ 显式映射为公开 DTO，避免泄漏实体内部结构
        var result = history.Select(a => new EntityHistoryDto(
            a.AuditId,
            a.EntityId,
            a.EntityType,
            a.AttributeId,
            a.AttributeName,
            a.OldValue,
            a.NewValue,
            a.ChangeType,
            a.ChangedBy,
            a.ChangedAt,
            a.CorrelationId,
            a.ClientIp)).ToList();

        return Ok(result);
    }

    /// <summary>
    /// ★ #7：删除实体（物理删除所有属性值 + 自定义表行）。
    /// 实体不存在时返回 404。
    /// </summary>
    [HttpDelete("entities/{id}")]
    public async Task<IActionResult> Delete(
        string id, string entityType,
        CancellationToken ct)
    {
        var deleted = await _write.DeleteEntityAsync(
            id, entityType,
            User.Identity?.Name ?? "system",
            HttpContext.TraceIdentifier,
            ct);

        if (!deleted) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// ★ PATCH 部分更新。
    ///
    /// 语义（与 PUT 的唯一差异）：
    ///   - values 中未出现的属性：**保持不变**（PUT 是删除）
    ///   - values 中值为 null 的属性：删除（与 PUT 一致）
    ///
    /// 乐观锁、未知属性检查、验证流程与 PUT 完全一致。
    /// </summary>
    [HttpPatch("entities/{id}")]
    public async Task<IActionResult> Patch(
        string id, string entityType,
        [FromBody] Dictionary<string, JsonElement> values,
        CancellationToken ct)
    {
        DateTimeOffset? expectedUpdatedAt = null;
        var headerValue = Request.Headers["X-Expected-Updated-At"].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerValue)
            && DateTimeOffset.TryParse(
                headerValue,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            expectedUpdatedAt = parsed;
        }

        try
        {
            var typedValues = ConvertJsonValues(entityType, values);
            await _write.PatchAsync(id, entityType, typedValues,
                User.Identity?.Name ?? "system",
                HttpContext.TraceIdentifier,
                ct,
                expectedUpdatedAt);
            return NoContent();
        }
        catch (EavConcurrencyException ex)
        {
            return Conflict(new
            {
                error = "并发冲突：实体已被其他用户修改，请刷新后重试",
                currentUpdatedAt = ex.CurrentUpdatedAt,
                expectedUpdatedAt = ex.ExpectedUpdatedAt
            });
        }
        catch (EavValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// ★ 批量删除实体。
    ///
    /// 用 POST（而非 DELETE with body）——某些代理会剥离 DELETE body。
    /// 语义：
    ///   - 请求体 { entityIds: [...] }
    ///   - 不存在的 ID 计入 NotFound，不报错
    ///   - 单事务完成所有删除 + 审计
    /// </summary>
    [HttpPost("entities/batch-delete")]
    public async Task<IActionResult> BatchDelete(
        string entityType, [FromBody] BatchDeleteRequest req,
        CancellationToken ct)
    {
        if (req.EntityIds.Count == 0)
        {
            return Ok(new BatchDeleteResultDto(
                Array.Empty<string>(), Array.Empty<string>(), 0));
        }

        var result = await _write.DeleteEntitiesAsync(
            req.EntityIds, entityType,
            User.Identity?.Name ?? "system",
            HttpContext.TraceIdentifier,
            ct);

        return Ok(new BatchDeleteResultDto(
            result.Deleted, result.NotFound, result.TotalAttributesDeleted));
    }

    // ---------- Schema 映射 ----------

    private AttributeSchemaDto ToSchemaDto(AttributeDefinition d)
    {
        CompositeTypeSchemaDto? composite = null;
        if (d.RefCompositeTypeId is not null)
            composite = BuildCompositeSchema(d.RefCompositeTypeId);

        UnitSchemaDto? unit = null;
        IReadOnlyList<UnitSchemaDto>? availableUnits = null;
        if (d.UnitId is { } uid)
        {
            try
            {
                var baseUnit = _unitCache.Get(uid);
                unit = ToUnitDto(baseUnit);
                availableUnits = _unitCache.GetByCategory(baseUnit.Category)
                    .Select(ToUnitDto).ToList();
            }
            catch (InvalidOperationException) { }
        }

        OptionSetSchemaDto? optionSet = null;
        if (d.DataType == EavDataTypes.SingleChoice && d.RefOptionSetId is { } osid)
        {
            try
            {
                var set = _optionSetCache.GetSet(osid);
                optionSet = new OptionSetSchemaDto(
                    set.OptionSetId, set.SetName, set.DisplayName,
                    set.Items.Select(i => new OptionItemSchemaDto(
                        i.OptionItemId, i.Value, i.Label, i.DisplayOrder, i.IsDefault))
                        .ToList());
            }
            catch (KeyNotFoundException) { }
        }

        return new AttributeSchemaDto(
            d.AttributeName, d.DisplayName, d.DataType,
            d.IsRequired, d.IsSearchable, d.IsSortable, d.DisplayOrder,
            d.AllowedValues?.RootElement.Clone(),
            d.ValidationRule?.RootElement.Clone(),
            composite, unit, availableUnits, optionSet,
            d.RefTableDefinitionId);
    }

    /// <summary>
    /// 递归构造组合类型 Schema。
    ///
    /// ★ #4：字段的 Unit / AvailableUnits 一并下放（仅 decimal 字段可能非空）。
    /// </summary>
    private CompositeTypeSchemaDto BuildCompositeSchema(string compositeTypeId)
    {
        var ct = _compositeCache.GetType(compositeTypeId);

        var fields = ct.Fields
            .Where(f => !f.IsDeleted)
            .OrderBy(f => f.DisplayOrder)
            .Select(BuildFieldSchema)
            .ToList();

        return new CompositeTypeSchemaDto(ct.TypeName, fields);
    }

    private CompositeFieldSchemaDto BuildFieldSchema(CompositeFieldDefinition f)
    {
        // ★ #4：解析单位
        UnitSchemaDto? unit = null;
        IReadOnlyList<UnitSchemaDto>? availableUnits = null;
        if (f.UnitId is { } uid)
        {
            try
            {
                var baseUnit = _unitCache.Get(uid);
                unit = ToUnitDto(baseUnit);
                availableUnits = _unitCache.GetByCategory(baseUnit.Category)
                    .Select(ToUnitDto).ToList();
            }
            catch (InvalidOperationException) { }
        }

        // ★ #8：选项集
        OptionSetSchemaDto? optionSet = null;
        if (f.DataType == EavDataTypes.SingleChoice && f.RefOptionSetId is { } osid)
        {
            try
            {
                var set = _optionSetCache.GetSet(osid);
                optionSet = new OptionSetSchemaDto(
                    set.OptionSetId, set.SetName, set.DisplayName,
                    set.Items.Select(i => new OptionItemSchemaDto(
                        i.OptionItemId, i.Value, i.Label, i.DisplayOrder, i.IsDefault))
                        .ToList());
            }
            catch (KeyNotFoundException) { }
        }

        return new CompositeFieldSchemaDto(
            f.FieldName, f.DisplayName, f.DataType,
            f.IsArray, f.IsRequired, f.IsSearchable, f.DisplayOrder,
            f.RefCompositeTypeId,
            f.DataType == EavDataTypes.Composite && f.RefCompositeTypeId is { } rid
                ? BuildCompositeSchema(rid)
                : null,
            f.ValidationRule?.RootElement.Clone(),
            f.AllowedValues?.RootElement.Clone(),
            unit,
            availableUnits,
            optionSet);   // ★ #8
    }

    private static UnitSchemaDto ToUnitDto(Unit u)
        => new(u.Id, u.Category, u.Name, u.Symbol, u.IsBaseUnit);

    // ---------- 请求体 JsonElement -> 强类型值 ----------

    private Dictionary<string, object?> ConvertJsonValues(
        string entityType, Dictionary<string, JsonElement> values)
    {
        var defs = _attrCache.GetDefinitions(entityType).ToDictionary(d => d.AttributeName);
        var result = new Dictionary<string, object?>();
        var tableAttrs = new List<string>();

        // ★ 未知属性不静默丢弃——收集后抛 400，防客户端拼写错误丢数据
        var unknownKeys = values.Keys.Where(k => !defs.ContainsKey(k)).ToList();
        if (unknownKeys.Count > 0)
        {
            throw new EavValidationException(unknownKeys
                .Select(k => new ValidationError(k, "未知属性"))
                .ToList());
        }

        foreach (var (name, elem) in values)
        {
            var def = defs[name];

            // ★ P2-3：table 类型属性不能通过 PUT entities 写入。
            //   行数据必须走 CustomTableDataController（PUT .../tables/{tableName}）。
            //   收集后统一抛错，避免静默忽略导致客户端误判"保存成功"。
            if (def.DataType == EavDataTypes.Table)
            {
                tableAttrs.Add(name);
                continue;
            }

            result[name] = ConvertJsonElement(elem, def);
        }

        if (tableAttrs.Count > 0)
        {
            throw new ArgumentException(
                $"table 类型属性不能通过 PUT entities 写入，请使用 " +
                $"PUT /api/eav/{{entityType}}/entities/{{entityId}}/tables/{{tableName}}。" +
                $"涉及属性: {string.Join(", ", tableAttrs)}");
        }

        return result;
    }

    private object? ConvertJsonElement(JsonElement elem, AttributeDefinition def)
    {
        if (elem.ValueKind == JsonValueKind.Null) return null;

        return def.DataType switch
        {
            EavDataTypes.Int or EavDataTypes.Decimal => ParseNumericJson(elem),
            EavDataTypes.Bool => elem.GetBoolean(),
            EavDataTypes.Datetime => elem.GetDateTimeOffset(),
            EavDataTypes.Date => DateOnly.Parse(elem.GetString()!),
            EavDataTypes.Time => TimeOnly.Parse(elem.GetString()!),
            EavDataTypes.String => elem.GetString(),
            EavDataTypes.Json => JsonDocument.Parse(elem.GetRawText()),
            EavDataTypes.File => JsonDocument.Parse(elem.GetRawText()),
            EavDataTypes.Composite => JsonToComposite(elem, def),
            _ => elem.GetString()
        };
    }

    private static NumericValue ParseNumericJson(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.Number)
            return new NumericValue(elem.GetDecimal(), null);

        if (elem.ValueKind == JsonValueKind.Object)
        {
            var value = elem.GetProperty("value").GetDecimal();
            Guid? unitId = elem.TryGetProperty("unitId", out var u)
                           && u.ValueKind == JsonValueKind.String
                ? Guid.Parse(u.GetString()!)
                : null;
            return new NumericValue(value, unitId);
        }
        throw new ArgumentException("数值格式错误");
    }

    private DynamicCompositeValue JsonToComposite(JsonElement elem, AttributeDefinition def)
    {
        using var doc = JsonDocument.Parse(elem.GetRawText());
        return CompositeFromDoc(doc, def.RefCompositeTypeId!);
    }

    private DynamicCompositeValue CompositeFromDoc(JsonDocument doc, string compositeTypeId)
    {
        var typeDef = _compositeCache.GetType(compositeTypeId);
        var result = new DynamicCompositeValue(typeDef.TypeName);
        var root = doc.RootElement;

        foreach (var field in typeDef.Fields.Where(f => !f.IsDeleted))
        {
            if (!root.TryGetProperty(field.FieldName, out var fe)) continue;
            if (fe.ValueKind == JsonValueKind.Null) continue;

            result[field.FieldName] = fe.ValueKind == JsonValueKind.Array && field.IsArray
                ? fe.EnumerateArray().Select(x => JsonElementToValue(x, field)).ToList()
                : JsonElementToValue(fe, field);
        }
        return result;
    }

    /// <summary>
    /// ★ #4：组合内 decimal 字段支持带单位对象 {value, unitId}；
    /// 其它字段保持原逻辑。
    /// </summary>
    private object? JsonElementToValue(JsonElement elem, CompositeFieldDefinition field)
    {
        if (elem.ValueKind == JsonValueKind.Null) return null;

        if (field.DataType == EavDataTypes.Composite)
        {
            using var nested = JsonDocument.Parse(elem.GetRawText());
            return CompositeFromDoc(nested, field.RefCompositeTypeId!);
        }

        // decimal 且带单位对象：返回 NumericValue 让下游统一处理
        if (field.DataType == EavDataTypes.Decimal && elem.ValueKind == JsonValueKind.Object)
        {
            var v = elem.GetProperty("value").GetDecimal();
            Guid? unitId = elem.TryGetProperty("unitId", out var u)
                            && u.ValueKind == JsonValueKind.String
                ? Guid.Parse(u.GetString()!)
                : null;
            return new NumericValue(v, unitId);
        }

        return field.DataType switch
        {
            EavDataTypes.Int => elem.GetInt64(),
            EavDataTypes.Decimal => elem.GetDecimal(),
            EavDataTypes.Bool => elem.GetBoolean(),
            EavDataTypes.Datetime => elem.GetDateTimeOffset(),
            EavDataTypes.Date => DateOnly.Parse(elem.GetString()!),
            EavDataTypes.Time => TimeOnly.Parse(elem.GetString()!),
            _ => elem.GetString()
        };
    }
}
```

## 文件 3/41 TreeGraph.Api/Controllers/EavEntityTypesController.cs

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Services;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Controllers;

/// <summary>
/// 实体类型元数据端点。
///
/// 独立于 EavController（其路由 api/eav/{entityType} 会与 api/eav/entity-types 冲突），
/// 因此拆到单独 Controller。
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

    /// <summary>列出所有已定义的实体类型（含属性计数）</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        // 直接从 AttributeCatalog 聚合
        var rows = await _db.AttributeCatalog
            .Where(a => !a.IsDeleted)
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
            .OrderBy(x => x.EntityType)
            .AsNoTracking()
            .ToListAsync(ct);

        var result = rows.Select(r => new EntityTypeSummaryDto(
            r.EntityType, r.Total, r.Searchable, r.FirstDisplay));

        return Ok(result);
    }
}
```

## 文件 4/41 TreeGraph.Api/Controllers/EavMetadataController.cs

```csharp
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

    // ============================================================
    // 属性定义
    // ============================================================

    /// <summary>
    /// 创建属性定义。
    ///
    /// ★ 单位绑定约束：只有 decimal 类型可以绑定 UnitId。
    /// int 类型拒绝绑定，因为归一化到基准单位时会产生小数（如 150 cm → 1.5 m），
    /// 写入 ValueInt (bigint) 会静默截断，造成数据损坏。
    /// </summary>
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

        // ★ 单位只允许 decimal
        if (req.UnitId is not null && req.DataType != EavDataTypes.Decimal)
        {
            return BadRequest(new
            {
                error = "只有 decimal 类型可以绑定单位。" +
                        "int 类型归一化到基准单位时会产生小数（如 150 cm → 1.5 m），" +
                        "写入 bigint 列会静默截断，请改用 decimal。"
            });
        }

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
    /// ★ 单位绑定约束：只有 decimal 类型可以绑定 UnitId。
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
            if (def.DataType != EavDataTypes.Decimal)
            {
                return BadRequest(new
                {
                    error = "只有 decimal 类型可以绑定单位。" +
                            "int 类型归一化到基准单位时会产生小数（如 150 cm → 1.5 m），" +
                            "写入 bigint 列会静默截断。"
                });
            }

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
```

## 文件 5/41 TreeGraph.Api/Controllers/OptionItemsController.cs

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Api.Services;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Controllers;

[ApiController]
[Route("api/eav/metadata/option-sets/{setId}/items")]
public class OptionItemsController : ControllerBase
{
    private readonly EavDbContext _db;
    private readonly IOptionSetCache _optionSetCache;

    public OptionItemsController(EavDbContext db, IOptionSetCache optionSetCache)
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
```

## 文件 6/41 TreeGraph.Api/Controllers/OptionSetsController.cs

```csharp
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
```

## 文件 7/41 TreeGraph.Api/Controllers/UnitsController.cs

```csharp
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

    /// <summary>
    /// 单次同步重算的行数上限。超过时拒绝并建议离线处理。
    /// 取值依据：ExecuteUpdateAsync 处理 20 万行约 2–4s，
    /// 事务锁持有 &lt; 5s，稳定落在 Polly AttemptTimeout（30s）内。
    /// </summary>
    private const int SyncRecalculateLimit = 200_000;

    public UnitsController(EavDbContext db, IUnitCache unitCache)
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
```

## 文件 8/41 TreeGraph.Api/Data/EavDbContext.cs

```csharp
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Entities;

namespace TreeGraph.Api.Data;

public class EavDbContext : DbContext
{
    public DbSet<AttributeDefinition> AttributeCatalog => Set<AttributeDefinition>();
    public DbSet<AttributeValue> AttributeValues => Set<AttributeValue>();
    public DbSet<CompositeTypeDefinition> CompositeTypes => Set<CompositeTypeDefinition>();
    public DbSet<CompositeFieldDefinition> CompositeFields => Set<CompositeFieldDefinition>();
    public DbSet<AttributeAuditLog> AttributeAuditLogs => Set<AttributeAuditLog>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<OptionSet> OptionSets => Set<OptionSet>();
    public DbSet<OptionItem> OptionItems => Set<OptionItem>();
    public DbSet<CustomTableDefinition> CustomTables => Set<CustomTableDefinition>();
    public DbSet<CustomTableColumn> CustomTableColumns => Set<CustomTableColumn>();
    public DbSet<CustomTableRow> CustomTableRows => Set<CustomTableRow>();

    public EavDbContext(DbContextOptions<EavDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        ConfigureUnits(mb);
        ConfigureOptionSets(mb);
        ConfigureAttributeCatalog(mb);
        ConfigureAttributeValues(mb);
        ConfigureCompositeTypes(mb);
        ConfigureCustomTables(mb);
        ConfigureAuditLog(mb);
    }

    private static void ConfigureUnits(ModelBuilder mb)
    {
        mb.Entity<Unit>(e =>
        {
            e.ToTable("units");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Category).HasColumnName("category").HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            e.Property(x => x.Symbol).HasColumnName("symbol").HasMaxLength(20).IsRequired();
            e.Property(x => x.ToBaseFactor).HasColumnName("to_base_factor").HasPrecision(38, 15);
            e.Property(x => x.IsBaseUnit).HasColumnName("is_base_unit");
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => new { x.Category, x.Name })
                .IsUnique()
                .HasDatabaseName("uq_unit_category_name");

            e.HasIndex(x => x.Category)
                .IsUnique()
                .HasDatabaseName("uq_unit_category_base")
                .HasFilter("is_base_unit = true");
        });
    }

    private static void ConfigureAttributeCatalog(ModelBuilder mb)
    {
        mb.Entity<AttributeDefinition>(e =>
        {
            // ★ 表级 CHECK 约束：int 类型不允许绑定单位
            // 归一化到基准单位会产生小数（150 cm → 1.5 m），写入 bigint 会静默截断。
            // 元数据层（Controller）与运行时层（EavValidationService）已双重拦截，
            // 这里再加一道数据库层防线，杜绝直接 SQL / 旧工具绕过。
            e.ToTable("attribute_catalog", t =>
            {
                t.HasCheckConstraint(
                    "ck_attr_int_no_unit",
                    "data_type <> 'int' OR unit_id IS NULL");
            });

            e.HasKey(x => x.AttributeId);
            e.Property(x => x.AttributeId)
                .HasColumnName("attribute_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.AttributeName).HasColumnName("attribute_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.IsRequired).HasColumnName("is_required");
            e.Property(x => x.IsSearchable).HasColumnName("is_searchable");
            e.Property(x => x.IsSortable).HasColumnName("is_sortable");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.DefaultValue).HasColumnName("default_value").HasMaxLength(500);

            e.Property(x => x.AllowedValues).HasColumnName("allowed_values").HasColumnType("jsonb");
            e.Property(x => x.ValidationRule).HasColumnName("validation_rule").HasColumnType("jsonb");
            e.Property(x => x.RefCompositeTypeId).HasColumnName("ref_composite_type_id").HasMaxLength(36);
            e.Property(x => x.RefTableDefinitionId).HasColumnName("ref_table_definition_id").HasMaxLength(36);
            e.Property(x => x.RefOptionSetId).HasColumnName("ref_option_set_id").HasMaxLength(36);
            e.Property(x => x.UnitId).HasColumnName("unit_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => new { x.EntityType, x.AttributeName })
                .IsUnique()
                .HasDatabaseName("uq_attr_catalog");

            e.HasIndex(x => x.EntityType)
                .HasDatabaseName("ix_attr_catalog_entity")
                .HasFilter("is_deleted = false");

            e.HasOne(x => x.RefCompositeType)
                .WithMany().HasForeignKey(x => x.RefCompositeTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Unit).WithMany()
                .HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.RefTableDefinition).WithMany()
                .HasForeignKey(x => x.RefTableDefinitionId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.RefOptionSet).WithMany()
                .HasForeignKey(x => x.RefOptionSetId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAttributeValues(ModelBuilder mb)
    {
        mb.Entity<AttributeValue>(e =>
        {
            e.ToTable("attribute_values");
            e.HasKey(x => x.ValueId);
            e.Property(x => x.ValueId)
                .HasColumnName("value_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.AttributeId).HasColumnName("attribute_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.ValueString).HasColumnName("value_string").HasMaxLength(2000);
            e.Property(x => x.ValueInt).HasColumnName("value_int");

            // ★ 修复 P1-1：value_decimal 精度从 (18,4) 提升到 (38,15)，
            // 与 units.to_base_factor 一致，避免单位换算（如 1 mg → kg）截断为 0
            e.Property(x => x.ValueDecimal)
                .HasColumnName("value_decimal")
                .HasPrecision(38, 15);

            e.Property(x => x.ValueBool).HasColumnName("value_bool");
            e.Property(x => x.ValueDatetime).HasColumnName("value_datetime").HasColumnType("timestamptz");
            e.Property(x => x.ValueDateOnly).HasColumnName("value_dateonly").HasColumnType("date");
            e.Property(x => x.ValueTime).HasColumnName("value_time").HasColumnType("time(0)");
            e.Property(x => x.ValueFileMeta).HasColumnName("value_file_meta").HasColumnType("jsonb");
            e.Property(x => x.ValueJsonb).HasColumnName("value_jsonb").HasColumnType("jsonb");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.UnitId).HasColumnName("unit_id");

            e.HasIndex(x => new { x.EntityId, x.EntityType, x.AttributeId })
                .IsUnique().HasDatabaseName("uq_av_entity_attr");

            e.HasIndex(x => new { x.EntityType, x.EntityId })
                .HasDatabaseName("ix_av_entity");

            // ★ #3 冲突检测：加速 max(UpdatedAt) 查询
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.UpdatedAt })
                .HasDatabaseName("ix_av_entity_updated")
                .IsDescending(false, false, true);

            e.HasIndex(x => new { x.AttributeId, x.ValueInt })
                .HasDatabaseName("ix_av_attr_int").HasFilter("value_int IS NOT NULL");
            e.HasIndex(x => new { x.AttributeId, x.ValueDecimal })
                .HasDatabaseName("ix_av_attr_decimal").HasFilter("value_decimal IS NOT NULL");
            e.HasIndex(x => new { x.AttributeId, x.ValueString })
                .HasDatabaseName("ix_av_attr_string").HasFilter("value_string IS NOT NULL");
            e.HasIndex(x => new { x.AttributeId, x.ValueBool })
                .HasDatabaseName("ix_av_attr_bool").HasFilter("value_bool IS NOT NULL");
            e.HasIndex(x => new { x.AttributeId, x.ValueDatetime })
                .HasDatabaseName("ix_av_attr_datetime").HasFilter("value_datetime IS NOT NULL");
            e.HasIndex(x => new { x.AttributeId, x.ValueDateOnly })
                .HasDatabaseName("ix_av_attr_dateonly").HasFilter("value_dateonly IS NOT NULL");
            e.HasIndex(x => new { x.AttributeId, x.ValueTime })
                .HasDatabaseName("ix_av_attr_time").HasFilter("value_time IS NOT NULL");

            e.HasIndex(x => x.ValueJsonb)
                .HasDatabaseName("ix_av_jsonb").HasMethod("GIN")
                .HasFilter("value_jsonb IS NOT NULL");
            e.HasIndex(x => x.ValueFileMeta)
                .HasDatabaseName("ix_av_file_meta").HasMethod("GIN")
                .HasFilter("value_file_meta IS NOT NULL");

            e.HasOne(x => x.Attribute).WithMany()
                .HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Unit).WithMany()
                .HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCompositeTypes(ModelBuilder mb)
    {
        mb.Entity<CompositeTypeDefinition>(e =>
        {
            e.ToTable("composite_type_definitions");
            e.HasKey(x => x.CompositeTypeId);
            e.Property(x => x.CompositeTypeId)
                .HasColumnName("composite_type_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.TypeName).HasColumnName("type_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => new { x.EntityType, x.TypeName, x.Version })
                .IsUnique().HasDatabaseName("uq_composite_type");
        });

        mb.Entity<CompositeFieldDefinition>(e =>
        {
            // CHECK：decimal 才能绑单位；single_choice 才能绑选项集
            e.ToTable("composite_field_definitions", t =>
            {
                t.HasCheckConstraint(
                    "ck_composite_field_decimal_unit",
                    "data_type = 'decimal' OR unit_id IS NULL");

                // ★ #8：single_choice 才能绑选项集
                t.HasCheckConstraint(
                    "ck_composite_field_single_choice_optionset",
                    "data_type = 'single_choice' OR ref_option_set_id IS NULL");
            });

            e.HasKey(x => x.FieldId);
            e.Property(x => x.FieldId)
                .HasColumnName("field_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.CompositeTypeId).HasColumnName("composite_type_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.FieldName).HasColumnName("field_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.RefCompositeTypeId).HasColumnName("ref_composite_type_id").HasMaxLength(36);

            // ★ #4：单位外键列
            e.Property(x => x.UnitId).HasColumnName("unit_id");
            // ★ #8：选项集外键列
            e.Property(x => x.RefOptionSetId).HasColumnName("ref_option_set_id").HasMaxLength(36);

            e.Property(x => x.IsArray).HasColumnName("is_array");
            e.Property(x => x.IsRequired).HasColumnName("is_required");
            e.Property(x => x.IsSearchable).HasColumnName("is_searchable");
            e.Property(x => x.IsSortable).HasColumnName("is_sortable");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.DefaultValue).HasColumnName("default_value").HasMaxLength(500);
            e.Property(x => x.ValidationRule).HasColumnName("validation_rule").HasColumnType("jsonb");
            e.Property(x => x.AllowedValues).HasColumnName("allowed_values").HasColumnType("jsonb");

            e.HasIndex(x => new { x.CompositeTypeId, x.FieldName })
                .IsUnique().HasDatabaseName("uq_composite_field");

            e.HasOne(x => x.CompositeType).WithMany(t => t.Fields)
                .HasForeignKey(x => x.CompositeTypeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.RefCompositeType).WithMany()
                .HasForeignKey(x => x.RefCompositeTypeId).OnDelete(DeleteBehavior.Restrict);

            // ★ #4：单位外键（Restrict）
            e.HasOne(x => x.Unit).WithMany()
                .HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);

            // ★ #8：选项集外键（Restrict）
            e.HasOne(x => x.RefOptionSet).WithMany()
                .HasForeignKey(x => x.RefOptionSetId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOptionSets(ModelBuilder mb)
    {
        mb.Entity<OptionSet>(e =>
        {
            e.ToTable("option_sets");
            e.HasKey(x => x.OptionSetId);
            e.Property(x => x.OptionSetId)
                .HasColumnName("option_set_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.SetName).HasColumnName("set_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");   // ★ 新增

            // ★ 改为 partial unique index：软删的集合不占用 SetName
            e.HasIndex(x => new { x.EntityType, x.SetName })
                .IsUnique()
                .HasDatabaseName("uq_option_set")
                .HasFilter("is_deleted = false");
        });

        mb.Entity<OptionItem>(e =>
        {
            e.ToTable("option_items");
            e.HasKey(x => x.OptionItemId);
            e.Property(x => x.OptionItemId)
                .HasColumnName("option_item_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.OptionSetId).HasColumnName("option_set_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.Value).HasColumnName("value").HasMaxLength(200).IsRequired();
            e.Property(x => x.Label).HasColumnName("label").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.IsDefault).HasColumnName("is_default");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");

            e.HasIndex(x => new { x.OptionSetId, x.Value })
                .IsUnique().HasDatabaseName("uq_option_item_value");
            e.HasIndex(x => x.OptionSetId).HasDatabaseName("ix_option_items_set");

            e.HasOne(x => x.OptionSet).WithMany(s => s.Items)
                .HasForeignKey(x => x.OptionSetId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureCustomTables(ModelBuilder mb)
    {
        mb.Entity<CustomTableDefinition>(e =>
        {
            e.ToTable("custom_table_definitions");
            e.HasKey(x => x.TableDefinitionId);
            e.Property(x => x.TableDefinitionId)
                .HasColumnName("table_definition_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.TableName).HasColumnName("table_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => new { x.EntityType, x.TableName, x.Version })
                .IsUnique().HasDatabaseName("uq_custom_table");
        });

        mb.Entity<CustomTableColumn>(e =>
        {
            e.ToTable("custom_table_columns");
            e.HasKey(x => x.ColumnId);
            e.Property(x => x.ColumnId)
                .HasColumnName("column_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.TableDefinitionId).HasColumnName("table_definition_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.ColumnName).HasColumnName("column_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.RefCompositeTypeId).HasColumnName("ref_composite_type_id").HasMaxLength(36);
            e.Property(x => x.IsRequired).HasColumnName("is_required");
            e.Property(x => x.IsSearchable).HasColumnName("is_searchable");
            e.Property(x => x.IsSortable).HasColumnName("is_sortable");
            e.Property(x => x.IsUnique).HasColumnName("is_unique");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.DefaultValue).HasColumnName("default_value").HasMaxLength(500);
            e.Property(x => x.ValidationRule).HasColumnName("validation_rule").HasColumnType("jsonb");
            e.Property(x => x.AllowedValues).HasColumnName("allowed_values").HasColumnType("jsonb");

            e.HasIndex(x => new { x.TableDefinitionId, x.ColumnName })
                .IsUnique().HasDatabaseName("uq_custom_table_column");

            e.HasOne(x => x.Table).WithMany(t => t.Columns)
                .HasForeignKey(x => x.TableDefinitionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.RefCompositeType).WithMany()
                .HasForeignKey(x => x.RefCompositeTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<CustomTableRow>(e =>
        {
            e.ToTable("custom_table_rows");
            e.HasKey(x => x.RowId);
            e.Property(x => x.RowId)
                .HasColumnName("row_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.TableDefinitionId).HasColumnName("table_definition_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.AttributeId).HasColumnName("attribute_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.ParentEntityId).HasColumnName("parent_entity_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.ParentEntityType).HasColumnName("parent_entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.RowData).HasColumnName("row_data").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.RowOrder).HasColumnName("row_order");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => new { x.ParentEntityType, x.ParentEntityId, x.AttributeId })
                .HasDatabaseName("ix_ctr_parent");
            e.HasIndex(x => x.TableDefinitionId).HasDatabaseName("ix_ctr_table");
            e.HasIndex(x => x.RowData).HasDatabaseName("ix_ctr_rowdata").HasMethod("GIN");
            e.HasIndex(x => new { x.ParentEntityId, x.AttributeId, x.RowOrder })
                .HasDatabaseName("ix_ctr_order");

            e.HasOne(x => x.Attribute).WithMany()
                .HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Table).WithMany()
                .HasForeignKey(x => x.TableDefinitionId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAuditLog(ModelBuilder mb)
    {
        mb.Entity<AttributeAuditLog>(e =>
        {
            e.ToTable("attribute_audit_log");
            e.HasKey(x => x.AuditId);
            e.Property(x => x.AuditId)
                .HasColumnName("audit_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.AttributeId).HasColumnName("attribute_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.AttributeName).HasColumnName("attribute_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.OldValue).HasColumnName("old_value");
            e.Property(x => x.NewValue).HasColumnName("new_value");
            e.Property(x => x.ChangeType).HasColumnName("change_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.ChangedBy).HasColumnName("changed_by").HasMaxLength(200).IsRequired();
            e.Property(x => x.ChangedAt).HasColumnName("changed_at");
            e.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
            e.Property(x => x.ClientIp).HasColumnName("client_ip").HasMaxLength(50);

            e.HasIndex(x => new { x.EntityType, x.EntityId, x.ChangedAt })
                .HasDatabaseName("ix_audit_entity")
                .IsDescending(false, false, true);
            e.HasIndex(x => x.ChangedAt).HasDatabaseName("ix_audit_time");
        });
    }
}
```

## 文件 9/41 TreeGraph.Api/Data/Migrations/20261002100058_Initial.cs

```csharp
using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attribute_audit_log",
                columns: table => new
                {
                    audit_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    attribute_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    attribute_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    change_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    changed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    client_ip = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_audit_log", x => x.audit_id);
                });

            migrationBuilder.CreateTable(
                name: "composite_type_definitions",
                columns: table => new
                {
                    composite_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_composite_type_definitions", x => x.composite_type_id);
                });

            migrationBuilder.CreateTable(
                name: "custom_table_definitions",
                columns: table => new
                {
                    table_definition_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    table_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_table_definitions", x => x.table_definition_id);
                });

            migrationBuilder.CreateTable(
                name: "option_sets",
                columns: table => new
                {
                    option_set_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    set_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_option_sets", x => x.option_set_id);
                });

            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    to_base_factor = table.Column<decimal>(type: "numeric(38,15)", precision: 38, scale: 15, nullable: false),
                    is_base_unit = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "custom_table_columns",
                columns: table => new
                {
                    column_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    table_definition_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    column_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    data_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ref_composite_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    is_searchable = table.Column<bool>(type: "boolean", nullable: false),
                    is_sortable = table.Column<bool>(type: "boolean", nullable: false),
                    is_unique = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    validation_rule = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    allowed_values = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    default_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_table_columns", x => x.column_id);
                    table.ForeignKey(
                        name: "FK_custom_table_columns_composite_type_definitions_ref_composi~",
                        column: x => x.ref_composite_type_id,
                        principalTable: "composite_type_definitions",
                        principalColumn: "composite_type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_custom_table_columns_custom_table_definitions_table_definit~",
                        column: x => x.table_definition_id,
                        principalTable: "custom_table_definitions",
                        principalColumn: "table_definition_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "option_items",
                columns: table => new
                {
                    option_item_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    option_set_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_option_items", x => x.option_item_id);
                    table.ForeignKey(
                        name: "FK_option_items_option_sets_option_set_id",
                        column: x => x.option_set_id,
                        principalTable: "option_sets",
                        principalColumn: "option_set_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attribute_catalog",
                columns: table => new
                {
                    attribute_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    attribute_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    data_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    is_searchable = table.Column<bool>(type: "boolean", nullable: false),
                    is_sortable = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    allowed_values = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    validation_rule = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    default_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ref_composite_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    ref_table_definition_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    ref_option_set_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_catalog", x => x.attribute_id);
                    table.CheckConstraint("ck_attr_int_no_unit", "data_type <> 'int' OR unit_id IS NULL");
                    table.ForeignKey(
                        name: "FK_attribute_catalog_composite_type_definitions_ref_composite_~",
                        column: x => x.ref_composite_type_id,
                        principalTable: "composite_type_definitions",
                        principalColumn: "composite_type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attribute_catalog_custom_table_definitions_ref_table_defini~",
                        column: x => x.ref_table_definition_id,
                        principalTable: "custom_table_definitions",
                        principalColumn: "table_definition_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attribute_catalog_option_sets_ref_option_set_id",
                        column: x => x.ref_option_set_id,
                        principalTable: "option_sets",
                        principalColumn: "option_set_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attribute_catalog_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "composite_field_definitions",
                columns: table => new
                {
                    field_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    composite_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    field_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    data_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ref_composite_type_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ref_option_set_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    is_array = table.Column<bool>(type: "boolean", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    is_searchable = table.Column<bool>(type: "boolean", nullable: false),
                    is_sortable = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    validation_rule = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    allowed_values = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    default_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_composite_field_definitions", x => x.field_id);
                    table.CheckConstraint("ck_composite_field_decimal_unit", "data_type = 'decimal' OR unit_id IS NULL");
                    table.CheckConstraint("ck_composite_field_single_choice_optionset", "data_type = 'single_choice' OR ref_option_set_id IS NULL");
                    table.ForeignKey(
                        name: "FK_composite_field_definitions_composite_type_definitions_comp~",
                        column: x => x.composite_type_id,
                        principalTable: "composite_type_definitions",
                        principalColumn: "composite_type_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_composite_field_definitions_composite_type_definitions_ref_~",
                        column: x => x.ref_composite_type_id,
                        principalTable: "composite_type_definitions",
                        principalColumn: "composite_type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_composite_field_definitions_option_sets_ref_option_set_id",
                        column: x => x.ref_option_set_id,
                        principalTable: "option_sets",
                        principalColumn: "option_set_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_composite_field_definitions_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attribute_values",
                columns: table => new
                {
                    value_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    entity_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    attribute_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    value_string = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    value_int = table.Column<long>(type: "bigint", nullable: true),
                    value_decimal = table.Column<decimal>(type: "numeric(38,15)", precision: 38, scale: 15, nullable: true),
                    value_bool = table.Column<bool>(type: "boolean", nullable: true),
                    value_datetime = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    value_dateonly = table.Column<DateOnly>(type: "date", nullable: true),
                    value_time = table.Column<TimeOnly>(type: "time(0) without time zone", nullable: true),
                    value_file_meta = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    value_jsonb = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_values", x => x.value_id);
                    table.ForeignKey(
                        name: "FK_attribute_values_attribute_catalog_attribute_id",
                        column: x => x.attribute_id,
                        principalTable: "attribute_catalog",
                        principalColumn: "attribute_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attribute_values_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "custom_table_rows",
                columns: table => new
                {
                    row_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false, defaultValueSql: "gen_random_uuid()::text"),
                    table_definition_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    attribute_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    parent_entity_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    parent_entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    row_data = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    row_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_table_rows", x => x.row_id);
                    table.ForeignKey(
                        name: "FK_custom_table_rows_attribute_catalog_attribute_id",
                        column: x => x.attribute_id,
                        principalTable: "attribute_catalog",
                        principalColumn: "attribute_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_custom_table_rows_custom_table_definitions_table_definition~",
                        column: x => x.table_definition_id,
                        principalTable: "custom_table_definitions",
                        principalColumn: "table_definition_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entity",
                table: "attribute_audit_log",
                columns: new[] { "entity_type", "entity_id", "changed_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_time",
                table: "attribute_audit_log",
                column: "changed_at");

            migrationBuilder.CreateIndex(
                name: "ix_attr_catalog_entity",
                table: "attribute_catalog",
                column: "entity_type",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_attribute_catalog_ref_composite_type_id",
                table: "attribute_catalog",
                column: "ref_composite_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_attribute_catalog_ref_option_set_id",
                table: "attribute_catalog",
                column: "ref_option_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_attribute_catalog_ref_table_definition_id",
                table: "attribute_catalog",
                column: "ref_table_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_attribute_catalog_unit_id",
                table: "attribute_catalog",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "uq_attr_catalog",
                table: "attribute_catalog",
                columns: new[] { "entity_type", "attribute_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attribute_values_unit_id",
                table: "attribute_values",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_av_attr_bool",
                table: "attribute_values",
                columns: new[] { "attribute_id", "value_bool" },
                filter: "value_bool IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_av_attr_dateonly",
                table: "attribute_values",
                columns: new[] { "attribute_id", "value_dateonly" },
                filter: "value_dateonly IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_av_attr_datetime",
                table: "attribute_values",
                columns: new[] { "attribute_id", "value_datetime" },
                filter: "value_datetime IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_av_attr_decimal",
                table: "attribute_values",
                columns: new[] { "attribute_id", "value_decimal" },
                filter: "value_decimal IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_av_attr_int",
                table: "attribute_values",
                columns: new[] { "attribute_id", "value_int" },
                filter: "value_int IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_av_attr_string",
                table: "attribute_values",
                columns: new[] { "attribute_id", "value_string" },
                filter: "value_string IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_av_attr_time",
                table: "attribute_values",
                columns: new[] { "attribute_id", "value_time" },
                filter: "value_time IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_av_entity",
                table: "attribute_values",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_av_entity_updated",
                table: "attribute_values",
                columns: new[] { "entity_type", "entity_id", "updated_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_av_file_meta",
                table: "attribute_values",
                column: "value_file_meta",
                filter: "value_file_meta IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_av_jsonb",
                table: "attribute_values",
                column: "value_jsonb",
                filter: "value_jsonb IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "uq_av_entity_attr",
                table: "attribute_values",
                columns: new[] { "entity_id", "entity_type", "attribute_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_composite_field_definitions_ref_composite_type_id",
                table: "composite_field_definitions",
                column: "ref_composite_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_composite_field_definitions_ref_option_set_id",
                table: "composite_field_definitions",
                column: "ref_option_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_composite_field_definitions_unit_id",
                table: "composite_field_definitions",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "uq_composite_field",
                table: "composite_field_definitions",
                columns: new[] { "composite_type_id", "field_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_composite_type",
                table: "composite_type_definitions",
                columns: new[] { "entity_type", "type_name", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_custom_table_columns_ref_composite_type_id",
                table: "custom_table_columns",
                column: "ref_composite_type_id");

            migrationBuilder.CreateIndex(
                name: "uq_custom_table_column",
                table: "custom_table_columns",
                columns: new[] { "table_definition_id", "column_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_custom_table",
                table: "custom_table_definitions",
                columns: new[] { "entity_type", "table_name", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ctr_order",
                table: "custom_table_rows",
                columns: new[] { "parent_entity_id", "attribute_id", "row_order" });

            migrationBuilder.CreateIndex(
                name: "ix_ctr_parent",
                table: "custom_table_rows",
                columns: new[] { "parent_entity_type", "parent_entity_id", "attribute_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ctr_rowdata",
                table: "custom_table_rows",
                column: "row_data")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_ctr_table",
                table: "custom_table_rows",
                column: "table_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_custom_table_rows_attribute_id",
                table: "custom_table_rows",
                column: "attribute_id");

            migrationBuilder.CreateIndex(
                name: "ix_option_items_set",
                table: "option_items",
                column: "option_set_id");

            migrationBuilder.CreateIndex(
                name: "uq_option_item_value",
                table: "option_items",
                columns: new[] { "option_set_id", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_option_set",
                table: "option_sets",
                columns: new[] { "entity_type", "set_name" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "uq_unit_category_base",
                table: "units",
                column: "category",
                unique: true,
                filter: "is_base_unit = true");

            migrationBuilder.CreateIndex(
                name: "uq_unit_category_name",
                table: "units",
                columns: new[] { "category", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attribute_audit_log");

            migrationBuilder.DropTable(
                name: "attribute_values");

            migrationBuilder.DropTable(
                name: "composite_field_definitions");

            migrationBuilder.DropTable(
                name: "custom_table_columns");

            migrationBuilder.DropTable(
                name: "custom_table_rows");

            migrationBuilder.DropTable(
                name: "option_items");

            migrationBuilder.DropTable(
                name: "attribute_catalog");

            migrationBuilder.DropTable(
                name: "composite_type_definitions");

            migrationBuilder.DropTable(
                name: "custom_table_definitions");

            migrationBuilder.DropTable(
                name: "option_sets");

            migrationBuilder.DropTable(
                name: "units");
        }
    }
}
```

## 文件 10/41 TreeGraph.Api/Data/Migrations/20261002100058_Initial.Designer.cs

```csharp
// <auto-generated />
using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using TreeGraph.Api.Data;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    [DbContext(typeof(EavDbContext))]
    [Migration("20261002100058_Initial")]
    partial class Initial
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("TreeGraph.Api.Entities.AttributeAuditLog", b =>
                {
                    b.Property<string>("AuditId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("audit_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<string>("AttributeId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("attribute_id");

                    b.Property<string>("AttributeName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("attribute_name");

                    b.Property<string>("ChangeType")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("change_type");

                    b.Property<DateTimeOffset>("ChangedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("changed_at");

                    b.Property<string>("ChangedBy")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("changed_by");

                    b.Property<string>("ClientIp")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)")
                        .HasColumnName("client_ip");

                    b.Property<string>("CorrelationId")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("correlation_id");

                    b.Property<string>("EntityId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("entity_id");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("entity_type");

                    b.Property<string>("NewValue")
                        .HasColumnType("text")
                        .HasColumnName("new_value");

                    b.Property<string>("OldValue")
                        .HasColumnType("text")
                        .HasColumnName("old_value");

                    b.HasKey("AuditId");

                    b.HasIndex("ChangedAt")
                        .HasDatabaseName("ix_audit_time");

                    b.HasIndex("EntityType", "EntityId", "ChangedAt")
                        .IsDescending(false, false, true)
                        .HasDatabaseName("ix_audit_entity");

                    b.ToTable("attribute_audit_log", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.AttributeDefinition", b =>
                {
                    b.Property<string>("AttributeId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("attribute_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<JsonDocument>("AllowedValues")
                        .HasColumnType("jsonb")
                        .HasColumnName("allowed_values");

                    b.Property<string>("AttributeName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("attribute_name");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("DataType")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("data_type");

                    b.Property<string>("DefaultValue")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("default_value");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("display_name");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("entity_type");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<bool>("IsRequired")
                        .HasColumnType("boolean")
                        .HasColumnName("is_required");

                    b.Property<bool>("IsSearchable")
                        .HasColumnType("boolean")
                        .HasColumnName("is_searchable");

                    b.Property<bool>("IsSortable")
                        .HasColumnType("boolean")
                        .HasColumnName("is_sortable");

                    b.Property<string>("RefCompositeTypeId")
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("ref_composite_type_id");

                    b.Property<string>("RefOptionSetId")
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("ref_option_set_id");

                    b.Property<string>("RefTableDefinitionId")
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("ref_table_definition_id");

                    b.Property<Guid?>("UnitId")
                        .HasColumnType("uuid")
                        .HasColumnName("unit_id");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<JsonDocument>("ValidationRule")
                        .HasColumnType("jsonb")
                        .HasColumnName("validation_rule");

                    b.Property<int>("Version")
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("AttributeId");

                    b.HasIndex("EntityType")
                        .HasDatabaseName("ix_attr_catalog_entity")
                        .HasFilter("is_deleted = false");

                    b.HasIndex("RefCompositeTypeId");

                    b.HasIndex("RefOptionSetId");

                    b.HasIndex("RefTableDefinitionId");

                    b.HasIndex("UnitId");

                    b.HasIndex("EntityType", "AttributeName")
                        .IsUnique()
                        .HasDatabaseName("uq_attr_catalog");

                    b.ToTable("attribute_catalog", null, t =>
                        {
                            t.HasCheckConstraint("ck_attr_int_no_unit", "data_type <> 'int' OR unit_id IS NULL");
                        });
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.AttributeValue", b =>
                {
                    b.Property<string>("ValueId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("value_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<string>("AttributeId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("attribute_id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("EntityId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("entity_id");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("entity_type");

                    b.Property<Guid?>("UnitId")
                        .HasColumnType("uuid")
                        .HasColumnName("unit_id");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<bool?>("ValueBool")
                        .HasColumnType("boolean")
                        .HasColumnName("value_bool");

                    b.Property<DateOnly?>("ValueDateOnly")
                        .HasColumnType("date")
                        .HasColumnName("value_dateonly");

                    b.Property<DateTimeOffset?>("ValueDatetime")
                        .HasColumnType("timestamptz")
                        .HasColumnName("value_datetime");

                    b.Property<decimal?>("ValueDecimal")
                        .HasPrecision(38, 15)
                        .HasColumnType("numeric(38,15)")
                        .HasColumnName("value_decimal");

                    b.Property<JsonDocument>("ValueFileMeta")
                        .HasColumnType("jsonb")
                        .HasColumnName("value_file_meta");

                    b.Property<long?>("ValueInt")
                        .HasColumnType("bigint")
                        .HasColumnName("value_int");

                    b.Property<JsonDocument>("ValueJsonb")
                        .HasColumnType("jsonb")
                        .HasColumnName("value_jsonb");

                    b.Property<string>("ValueString")
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("value_string");

                    b.Property<TimeOnly?>("ValueTime")
                        .HasColumnType("time(0)")
                        .HasColumnName("value_time");

                    b.HasKey("ValueId");

                    b.HasIndex("UnitId");

                    b.HasIndex("ValueFileMeta")
                        .HasDatabaseName("ix_av_file_meta")
                        .HasFilter("value_file_meta IS NOT NULL");

                    NpgsqlIndexBuilderExtensions.HasMethod(b.HasIndex("ValueFileMeta"), "GIN");

                    b.HasIndex("ValueJsonb")
                        .HasDatabaseName("ix_av_jsonb")
                        .HasFilter("value_jsonb IS NOT NULL");

                    NpgsqlIndexBuilderExtensions.HasMethod(b.HasIndex("ValueJsonb"), "GIN");

                    b.HasIndex("AttributeId", "ValueBool")
                        .HasDatabaseName("ix_av_attr_bool")
                        .HasFilter("value_bool IS NOT NULL");

                    b.HasIndex("AttributeId", "ValueDateOnly")
                        .HasDatabaseName("ix_av_attr_dateonly")
                        .HasFilter("value_dateonly IS NOT NULL");

                    b.HasIndex("AttributeId", "ValueDatetime")
                        .HasDatabaseName("ix_av_attr_datetime")
                        .HasFilter("value_datetime IS NOT NULL");

                    b.HasIndex("AttributeId", "ValueDecimal")
                        .HasDatabaseName("ix_av_attr_decimal")
                        .HasFilter("value_decimal IS NOT NULL");

                    b.HasIndex("AttributeId", "ValueInt")
                        .HasDatabaseName("ix_av_attr_int")
                        .HasFilter("value_int IS NOT NULL");

                    b.HasIndex("AttributeId", "ValueString")
                        .HasDatabaseName("ix_av_attr_string")
                        .HasFilter("value_string IS NOT NULL");

                    b.HasIndex("AttributeId", "ValueTime")
                        .HasDatabaseName("ix_av_attr_time")
                        .HasFilter("value_time IS NOT NULL");

                    b.HasIndex("EntityType", "EntityId")
                        .HasDatabaseName("ix_av_entity");

                    b.HasIndex("EntityId", "EntityType", "AttributeId")
                        .IsUnique()
                        .HasDatabaseName("uq_av_entity_attr");

                    b.HasIndex("EntityType", "EntityId", "UpdatedAt")
                        .IsDescending(false, false, true)
                        .HasDatabaseName("ix_av_entity_updated");

                    b.ToTable("attribute_values", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CompositeFieldDefinition", b =>
                {
                    b.Property<string>("FieldId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("field_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<JsonDocument>("AllowedValues")
                        .HasColumnType("jsonb")
                        .HasColumnName("allowed_values");

                    b.Property<string>("CompositeTypeId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("composite_type_id");

                    b.Property<string>("DataType")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("data_type");

                    b.Property<string>("DefaultValue")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("default_value");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("display_name");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<string>("FieldName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("field_name");

                    b.Property<bool>("IsArray")
                        .HasColumnType("boolean")
                        .HasColumnName("is_array");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<bool>("IsRequired")
                        .HasColumnType("boolean")
                        .HasColumnName("is_required");

                    b.Property<bool>("IsSearchable")
                        .HasColumnType("boolean")
                        .HasColumnName("is_searchable");

                    b.Property<bool>("IsSortable")
                        .HasColumnType("boolean")
                        .HasColumnName("is_sortable");

                    b.Property<string>("RefCompositeTypeId")
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("ref_composite_type_id");

                    b.Property<string>("RefOptionSetId")
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("ref_option_set_id");

                    b.Property<Guid?>("UnitId")
                        .HasColumnType("uuid")
                        .HasColumnName("unit_id");

                    b.Property<JsonDocument>("ValidationRule")
                        .HasColumnType("jsonb")
                        .HasColumnName("validation_rule");

                    b.HasKey("FieldId");

                    b.HasIndex("RefCompositeTypeId");

                    b.HasIndex("RefOptionSetId");

                    b.HasIndex("UnitId");

                    b.HasIndex("CompositeTypeId", "FieldName")
                        .IsUnique()
                        .HasDatabaseName("uq_composite_field");

                    b.ToTable("composite_field_definitions", null, t =>
                        {
                            t.HasCheckConstraint("ck_composite_field_decimal_unit", "data_type = 'decimal' OR unit_id IS NULL");

                            t.HasCheckConstraint("ck_composite_field_single_choice_optionset", "data_type = 'single_choice' OR ref_option_set_id IS NULL");
                        });
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CompositeTypeDefinition", b =>
                {
                    b.Property<string>("CompositeTypeId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("composite_type_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("display_name");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("entity_type");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<string>("TypeName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("type_name");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<int>("Version")
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("CompositeTypeId");

                    b.HasIndex("EntityType", "TypeName", "Version")
                        .IsUnique()
                        .HasDatabaseName("uq_composite_type");

                    b.ToTable("composite_type_definitions", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CustomTableColumn", b =>
                {
                    b.Property<string>("ColumnId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("column_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<JsonDocument>("AllowedValues")
                        .HasColumnType("jsonb")
                        .HasColumnName("allowed_values");

                    b.Property<string>("ColumnName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("column_name");

                    b.Property<string>("DataType")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("data_type");

                    b.Property<string>("DefaultValue")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("default_value");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("display_name");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<bool>("IsRequired")
                        .HasColumnType("boolean")
                        .HasColumnName("is_required");

                    b.Property<bool>("IsSearchable")
                        .HasColumnType("boolean")
                        .HasColumnName("is_searchable");

                    b.Property<bool>("IsSortable")
                        .HasColumnType("boolean")
                        .HasColumnName("is_sortable");

                    b.Property<bool>("IsUnique")
                        .HasColumnType("boolean")
                        .HasColumnName("is_unique");

                    b.Property<string>("RefCompositeTypeId")
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("ref_composite_type_id");

                    b.Property<string>("TableDefinitionId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("table_definition_id");

                    b.Property<JsonDocument>("ValidationRule")
                        .HasColumnType("jsonb")
                        .HasColumnName("validation_rule");

                    b.HasKey("ColumnId");

                    b.HasIndex("RefCompositeTypeId");

                    b.HasIndex("TableDefinitionId", "ColumnName")
                        .IsUnique()
                        .HasDatabaseName("uq_custom_table_column");

                    b.ToTable("custom_table_columns", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CustomTableDefinition", b =>
                {
                    b.Property<string>("TableDefinitionId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("table_definition_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("display_name");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("entity_type");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<string>("TableName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("table_name");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<int>("Version")
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("TableDefinitionId");

                    b.HasIndex("EntityType", "TableName", "Version")
                        .IsUnique()
                        .HasDatabaseName("uq_custom_table");

                    b.ToTable("custom_table_definitions", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CustomTableRow", b =>
                {
                    b.Property<string>("RowId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("row_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<string>("AttributeId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("attribute_id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("ParentEntityId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("parent_entity_id");

                    b.Property<string>("ParentEntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("parent_entity_type");

                    b.Property<JsonDocument>("RowData")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasColumnName("row_data");

                    b.Property<int>("RowOrder")
                        .HasColumnType("integer")
                        .HasColumnName("row_order");

                    b.Property<string>("TableDefinitionId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("table_definition_id");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.HasKey("RowId");

                    b.HasIndex("AttributeId");

                    b.HasIndex("RowData")
                        .HasDatabaseName("ix_ctr_rowdata");

                    NpgsqlIndexBuilderExtensions.HasMethod(b.HasIndex("RowData"), "GIN");

                    b.HasIndex("TableDefinitionId")
                        .HasDatabaseName("ix_ctr_table");

                    b.HasIndex("ParentEntityId", "AttributeId", "RowOrder")
                        .HasDatabaseName("ix_ctr_order");

                    b.HasIndex("ParentEntityType", "ParentEntityId", "AttributeId")
                        .HasDatabaseName("ix_ctr_parent");

                    b.ToTable("custom_table_rows", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.OptionItem", b =>
                {
                    b.Property<string>("OptionItemId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("option_item_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<bool>("IsDefault")
                        .HasColumnType("boolean")
                        .HasColumnName("is_default");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<string>("Label")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("label");

                    b.Property<string>("OptionSetId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("option_set_id");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("value");

                    b.HasKey("OptionItemId");

                    b.HasIndex("OptionSetId")
                        .HasDatabaseName("ix_option_items_set");

                    b.HasIndex("OptionSetId", "Value")
                        .IsUnique()
                        .HasDatabaseName("uq_option_item_value");

                    b.ToTable("option_items", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.OptionSet", b =>
                {
                    b.Property<string>("OptionSetId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("option_set_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("display_name");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("entity_type");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<string>("SetName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("set_name");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.HasKey("OptionSetId");

                    b.HasIndex("EntityType", "SetName")
                        .IsUnique()
                        .HasDatabaseName("uq_option_set")
                        .HasFilter("is_deleted = false");

                    b.ToTable("option_sets", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.Unit", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Category")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)")
                        .HasColumnName("category");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<bool>("IsBaseUnit")
                        .HasColumnType("boolean")
                        .HasColumnName("is_base_unit");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("name");

                    b.Property<string>("Symbol")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("symbol");

                    b.Property<decimal>("ToBaseFactor")
                        .HasPrecision(38, 15)
                        .HasColumnType("numeric(38,15)")
                        .HasColumnName("to_base_factor");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.HasKey("Id");

                    b.HasIndex("Category")
                        .IsUnique()
                        .HasDatabaseName("uq_unit_category_base")
                        .HasFilter("is_base_unit = true");

                    b.HasIndex("Category", "Name")
                        .IsUnique()
                        .HasDatabaseName("uq_unit_category_name");

                    b.ToTable("units", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.AttributeDefinition", b =>
                {
                    b.HasOne("TreeGraph.Api.Entities.CompositeTypeDefinition", "RefCompositeType")
                        .WithMany()
                        .HasForeignKey("RefCompositeTypeId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("TreeGraph.Api.Entities.OptionSet", "RefOptionSet")
                        .WithMany()
                        .HasForeignKey("RefOptionSetId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("TreeGraph.Api.Entities.CustomTableDefinition", "RefTableDefinition")
                        .WithMany()
                        .HasForeignKey("RefTableDefinitionId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("TreeGraph.Api.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.Navigation("RefCompositeType");

                    b.Navigation("RefOptionSet");

                    b.Navigation("RefTableDefinition");

                    b.Navigation("Unit");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.AttributeValue", b =>
                {
                    b.HasOne("TreeGraph.Api.Entities.AttributeDefinition", "Attribute")
                        .WithMany()
                        .HasForeignKey("AttributeId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("TreeGraph.Api.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.Navigation("Attribute");

                    b.Navigation("Unit");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CompositeFieldDefinition", b =>
                {
                    b.HasOne("TreeGraph.Api.Entities.CompositeTypeDefinition", "CompositeType")
                        .WithMany("Fields")
                        .HasForeignKey("CompositeTypeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("TreeGraph.Api.Entities.CompositeTypeDefinition", "RefCompositeType")
                        .WithMany()
                        .HasForeignKey("RefCompositeTypeId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("TreeGraph.Api.Entities.OptionSet", "RefOptionSet")
                        .WithMany()
                        .HasForeignKey("RefOptionSetId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("TreeGraph.Api.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.Navigation("CompositeType");

                    b.Navigation("RefCompositeType");

                    b.Navigation("RefOptionSet");

                    b.Navigation("Unit");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CustomTableColumn", b =>
                {
                    b.HasOne("TreeGraph.Api.Entities.CompositeTypeDefinition", "RefCompositeType")
                        .WithMany()
                        .HasForeignKey("RefCompositeTypeId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("TreeGraph.Api.Entities.CustomTableDefinition", "Table")
                        .WithMany("Columns")
                        .HasForeignKey("TableDefinitionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("RefCompositeType");

                    b.Navigation("Table");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CustomTableRow", b =>
                {
                    b.HasOne("TreeGraph.Api.Entities.AttributeDefinition", "Attribute")
                        .WithMany()
                        .HasForeignKey("AttributeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("TreeGraph.Api.Entities.CustomTableDefinition", "Table")
                        .WithMany()
                        .HasForeignKey("TableDefinitionId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Attribute");

                    b.Navigation("Table");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.OptionItem", b =>
                {
                    b.HasOne("TreeGraph.Api.Entities.OptionSet", "OptionSet")
                        .WithMany("Items")
                        .HasForeignKey("OptionSetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("OptionSet");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CompositeTypeDefinition", b =>
                {
                    b.Navigation("Fields");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CustomTableDefinition", b =>
                {
                    b.Navigation("Columns");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.OptionSet", b =>
                {
                    b.Navigation("Items");
                });
#pragma warning restore 612, 618
        }
    }
}
```

## 文件 11/41 TreeGraph.Api/Data/Migrations/EavDbContextModelSnapshot.cs

```csharp
// <auto-generated />
using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using TreeGraph.Api.Data;

#nullable disable

namespace TreeGraph.Api.Data.Migrations
{
    [DbContext(typeof(EavDbContext))]
    partial class EavDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("TreeGraph.Api.Entities.AttributeAuditLog", b =>
                {
                    b.Property<string>("AuditId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("audit_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<string>("AttributeId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("attribute_id");

                    b.Property<string>("AttributeName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("attribute_name");

                    b.Property<string>("ChangeType")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("change_type");

                    b.Property<DateTimeOffset>("ChangedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("changed_at");

                    b.Property<string>("ChangedBy")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("changed_by");

                    b.Property<string>("ClientIp")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)")
                        .HasColumnName("client_ip");

                    b.Property<string>("CorrelationId")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("correlation_id");

                    b.Property<string>("EntityId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("entity_id");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("entity_type");

                    b.Property<string>("NewValue")
                        .HasColumnType("text")
                        .HasColumnName("new_value");

                    b.Property<string>("OldValue")
                        .HasColumnType("text")
                        .HasColumnName("old_value");

                    b.HasKey("AuditId");

                    b.HasIndex("ChangedAt")
                        .HasDatabaseName("ix_audit_time");

                    b.HasIndex("EntityType", "EntityId", "ChangedAt")
                        .IsDescending(false, false, true)
                        .HasDatabaseName("ix_audit_entity");

                    b.ToTable("attribute_audit_log", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.AttributeDefinition", b =>
                {
                    b.Property<string>("AttributeId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("attribute_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<JsonDocument>("AllowedValues")
                        .HasColumnType("jsonb")
                        .HasColumnName("allowed_values");

                    b.Property<string>("AttributeName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("attribute_name");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("DataType")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("data_type");

                    b.Property<string>("DefaultValue")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("default_value");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("display_name");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("entity_type");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<bool>("IsRequired")
                        .HasColumnType("boolean")
                        .HasColumnName("is_required");

                    b.Property<bool>("IsSearchable")
                        .HasColumnType("boolean")
                        .HasColumnName("is_searchable");

                    b.Property<bool>("IsSortable")
                        .HasColumnType("boolean")
                        .HasColumnName("is_sortable");

                    b.Property<string>("RefCompositeTypeId")
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("ref_composite_type_id");

                    b.Property<string>("RefOptionSetId")
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("ref_option_set_id");

                    b.Property<string>("RefTableDefinitionId")
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("ref_table_definition_id");

                    b.Property<Guid?>("UnitId")
                        .HasColumnType("uuid")
                        .HasColumnName("unit_id");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<JsonDocument>("ValidationRule")
                        .HasColumnType("jsonb")
                        .HasColumnName("validation_rule");

                    b.Property<int>("Version")
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("AttributeId");

                    b.HasIndex("EntityType")
                        .HasDatabaseName("ix_attr_catalog_entity")
                        .HasFilter("is_deleted = false");

                    b.HasIndex("RefCompositeTypeId");

                    b.HasIndex("RefOptionSetId");

                    b.HasIndex("RefTableDefinitionId");

                    b.HasIndex("UnitId");

                    b.HasIndex("EntityType", "AttributeName")
                        .IsUnique()
                        .HasDatabaseName("uq_attr_catalog");

                    b.ToTable("attribute_catalog", null, t =>
                        {
                            t.HasCheckConstraint("ck_attr_int_no_unit", "data_type <> 'int' OR unit_id IS NULL");
                        });
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.AttributeValue", b =>
                {
                    b.Property<string>("ValueId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("value_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<string>("AttributeId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("attribute_id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("EntityId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("entity_id");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("entity_type");

                    b.Property<Guid?>("UnitId")
                        .HasColumnType("uuid")
                        .HasColumnName("unit_id");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<bool?>("ValueBool")
                        .HasColumnType("boolean")
                        .HasColumnName("value_bool");

                    b.Property<DateOnly?>("ValueDateOnly")
                        .HasColumnType("date")
                        .HasColumnName("value_dateonly");

                    b.Property<DateTimeOffset?>("ValueDatetime")
                        .HasColumnType("timestamptz")
                        .HasColumnName("value_datetime");

                    b.Property<decimal?>("ValueDecimal")
                        .HasPrecision(38, 15)
                        .HasColumnType("numeric(38,15)")
                        .HasColumnName("value_decimal");

                    b.Property<JsonDocument>("ValueFileMeta")
                        .HasColumnType("jsonb")
                        .HasColumnName("value_file_meta");

                    b.Property<long?>("ValueInt")
                        .HasColumnType("bigint")
                        .HasColumnName("value_int");

                    b.Property<JsonDocument>("ValueJsonb")
                        .HasColumnType("jsonb")
                        .HasColumnName("value_jsonb");

                    b.Property<string>("ValueString")
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("value_string");

                    b.Property<TimeOnly?>("ValueTime")
                        .HasColumnType("time(0)")
                        .HasColumnName("value_time");

                    b.HasKey("ValueId");

                    b.HasIndex("UnitId");

                    b.HasIndex("ValueFileMeta")
                        .HasDatabaseName("ix_av_file_meta")
                        .HasFilter("value_file_meta IS NOT NULL");

                    NpgsqlIndexBuilderExtensions.HasMethod(b.HasIndex("ValueFileMeta"), "GIN");

                    b.HasIndex("ValueJsonb")
                        .HasDatabaseName("ix_av_jsonb")
                        .HasFilter("value_jsonb IS NOT NULL");

                    NpgsqlIndexBuilderExtensions.HasMethod(b.HasIndex("ValueJsonb"), "GIN");

                    b.HasIndex("AttributeId", "ValueBool")
                        .HasDatabaseName("ix_av_attr_bool")
                        .HasFilter("value_bool IS NOT NULL");

                    b.HasIndex("AttributeId", "ValueDateOnly")
                        .HasDatabaseName("ix_av_attr_dateonly")
                        .HasFilter("value_dateonly IS NOT NULL");

                    b.HasIndex("AttributeId", "ValueDatetime")
                        .HasDatabaseName("ix_av_attr_datetime")
                        .HasFilter("value_datetime IS NOT NULL");

                    b.HasIndex("AttributeId", "ValueDecimal")
                        .HasDatabaseName("ix_av_attr_decimal")
                        .HasFilter("value_decimal IS NOT NULL");

                    b.HasIndex("AttributeId", "ValueInt")
                        .HasDatabaseName("ix_av_attr_int")
                        .HasFilter("value_int IS NOT NULL");

                    b.HasIndex("AttributeId", "ValueString")
                        .HasDatabaseName("ix_av_attr_string")
                        .HasFilter("value_string IS NOT NULL");

                    b.HasIndex("AttributeId", "ValueTime")
                        .HasDatabaseName("ix_av_attr_time")
                        .HasFilter("value_time IS NOT NULL");

                    b.HasIndex("EntityType", "EntityId")
                        .HasDatabaseName("ix_av_entity");

                    b.HasIndex("EntityId", "EntityType", "AttributeId")
                        .IsUnique()
                        .HasDatabaseName("uq_av_entity_attr");

                    b.HasIndex("EntityType", "EntityId", "UpdatedAt")
                        .IsDescending(false, false, true)
                        .HasDatabaseName("ix_av_entity_updated");

                    b.ToTable("attribute_values", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CompositeFieldDefinition", b =>
                {
                    b.Property<string>("FieldId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("field_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<JsonDocument>("AllowedValues")
                        .HasColumnType("jsonb")
                        .HasColumnName("allowed_values");

                    b.Property<string>("CompositeTypeId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("composite_type_id");

                    b.Property<string>("DataType")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("data_type");

                    b.Property<string>("DefaultValue")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("default_value");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("display_name");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<string>("FieldName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("field_name");

                    b.Property<bool>("IsArray")
                        .HasColumnType("boolean")
                        .HasColumnName("is_array");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<bool>("IsRequired")
                        .HasColumnType("boolean")
                        .HasColumnName("is_required");

                    b.Property<bool>("IsSearchable")
                        .HasColumnType("boolean")
                        .HasColumnName("is_searchable");

                    b.Property<bool>("IsSortable")
                        .HasColumnType("boolean")
                        .HasColumnName("is_sortable");

                    b.Property<string>("RefCompositeTypeId")
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("ref_composite_type_id");

                    b.Property<string>("RefOptionSetId")
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("ref_option_set_id");

                    b.Property<Guid?>("UnitId")
                        .HasColumnType("uuid")
                        .HasColumnName("unit_id");

                    b.Property<JsonDocument>("ValidationRule")
                        .HasColumnType("jsonb")
                        .HasColumnName("validation_rule");

                    b.HasKey("FieldId");

                    b.HasIndex("RefCompositeTypeId");

                    b.HasIndex("RefOptionSetId");

                    b.HasIndex("UnitId");

                    b.HasIndex("CompositeTypeId", "FieldName")
                        .IsUnique()
                        .HasDatabaseName("uq_composite_field");

                    b.ToTable("composite_field_definitions", null, t =>
                        {
                            t.HasCheckConstraint("ck_composite_field_decimal_unit", "data_type = 'decimal' OR unit_id IS NULL");

                            t.HasCheckConstraint("ck_composite_field_single_choice_optionset", "data_type = 'single_choice' OR ref_option_set_id IS NULL");
                        });
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CompositeTypeDefinition", b =>
                {
                    b.Property<string>("CompositeTypeId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("composite_type_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("display_name");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("entity_type");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<string>("TypeName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("type_name");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<int>("Version")
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("CompositeTypeId");

                    b.HasIndex("EntityType", "TypeName", "Version")
                        .IsUnique()
                        .HasDatabaseName("uq_composite_type");

                    b.ToTable("composite_type_definitions", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CustomTableColumn", b =>
                {
                    b.Property<string>("ColumnId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("column_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<JsonDocument>("AllowedValues")
                        .HasColumnType("jsonb")
                        .HasColumnName("allowed_values");

                    b.Property<string>("ColumnName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("column_name");

                    b.Property<string>("DataType")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("data_type");

                    b.Property<string>("DefaultValue")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("default_value");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("display_name");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<bool>("IsRequired")
                        .HasColumnType("boolean")
                        .HasColumnName("is_required");

                    b.Property<bool>("IsSearchable")
                        .HasColumnType("boolean")
                        .HasColumnName("is_searchable");

                    b.Property<bool>("IsSortable")
                        .HasColumnType("boolean")
                        .HasColumnName("is_sortable");

                    b.Property<bool>("IsUnique")
                        .HasColumnType("boolean")
                        .HasColumnName("is_unique");

                    b.Property<string>("RefCompositeTypeId")
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("ref_composite_type_id");

                    b.Property<string>("TableDefinitionId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("table_definition_id");

                    b.Property<JsonDocument>("ValidationRule")
                        .HasColumnType("jsonb")
                        .HasColumnName("validation_rule");

                    b.HasKey("ColumnId");

                    b.HasIndex("RefCompositeTypeId");

                    b.HasIndex("TableDefinitionId", "ColumnName")
                        .IsUnique()
                        .HasDatabaseName("uq_custom_table_column");

                    b.ToTable("custom_table_columns", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CustomTableDefinition", b =>
                {
                    b.Property<string>("TableDefinitionId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("table_definition_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("display_name");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("entity_type");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<string>("TableName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("table_name");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<int>("Version")
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("TableDefinitionId");

                    b.HasIndex("EntityType", "TableName", "Version")
                        .IsUnique()
                        .HasDatabaseName("uq_custom_table");

                    b.ToTable("custom_table_definitions", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CustomTableRow", b =>
                {
                    b.Property<string>("RowId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("row_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<string>("AttributeId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("attribute_id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("ParentEntityId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("parent_entity_id");

                    b.Property<string>("ParentEntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("parent_entity_type");

                    b.Property<JsonDocument>("RowData")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasColumnName("row_data");

                    b.Property<int>("RowOrder")
                        .HasColumnType("integer")
                        .HasColumnName("row_order");

                    b.Property<string>("TableDefinitionId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("table_definition_id");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.HasKey("RowId");

                    b.HasIndex("AttributeId");

                    b.HasIndex("RowData")
                        .HasDatabaseName("ix_ctr_rowdata");

                    NpgsqlIndexBuilderExtensions.HasMethod(b.HasIndex("RowData"), "GIN");

                    b.HasIndex("TableDefinitionId")
                        .HasDatabaseName("ix_ctr_table");

                    b.HasIndex("ParentEntityId", "AttributeId", "RowOrder")
                        .HasDatabaseName("ix_ctr_order");

                    b.HasIndex("ParentEntityType", "ParentEntityId", "AttributeId")
                        .HasDatabaseName("ix_ctr_parent");

                    b.ToTable("custom_table_rows", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.OptionItem", b =>
                {
                    b.Property<string>("OptionItemId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("option_item_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<bool>("IsDefault")
                        .HasColumnType("boolean")
                        .HasColumnName("is_default");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<string>("Label")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("label");

                    b.Property<string>("OptionSetId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("option_set_id");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("value");

                    b.HasKey("OptionItemId");

                    b.HasIndex("OptionSetId")
                        .HasDatabaseName("ix_option_items_set");

                    b.HasIndex("OptionSetId", "Value")
                        .IsUnique()
                        .HasDatabaseName("uq_option_item_value");

                    b.ToTable("option_items", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.OptionSet", b =>
                {
                    b.Property<string>("OptionSetId")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("option_set_id")
                        .HasDefaultValueSql("gen_random_uuid()::text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("display_name");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("entity_type");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<string>("SetName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("set_name");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.HasKey("OptionSetId");

                    b.HasIndex("EntityType", "SetName")
                        .IsUnique()
                        .HasDatabaseName("uq_option_set")
                        .HasFilter("is_deleted = false");

                    b.ToTable("option_sets", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.Unit", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Category")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)")
                        .HasColumnName("category");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<int>("DisplayOrder")
                        .HasColumnType("integer")
                        .HasColumnName("display_order");

                    b.Property<bool>("IsBaseUnit")
                        .HasColumnType("boolean")
                        .HasColumnName("is_base_unit");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("name");

                    b.Property<string>("Symbol")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)")
                        .HasColumnName("symbol");

                    b.Property<decimal>("ToBaseFactor")
                        .HasPrecision(38, 15)
                        .HasColumnType("numeric(38,15)")
                        .HasColumnName("to_base_factor");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.HasKey("Id");

                    b.HasIndex("Category")
                        .IsUnique()
                        .HasDatabaseName("uq_unit_category_base")
                        .HasFilter("is_base_unit = true");

                    b.HasIndex("Category", "Name")
                        .IsUnique()
                        .HasDatabaseName("uq_unit_category_name");

                    b.ToTable("units", (string)null);
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.AttributeDefinition", b =>
                {
                    b.HasOne("TreeGraph.Api.Entities.CompositeTypeDefinition", "RefCompositeType")
                        .WithMany()
                        .HasForeignKey("RefCompositeTypeId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("TreeGraph.Api.Entities.OptionSet", "RefOptionSet")
                        .WithMany()
                        .HasForeignKey("RefOptionSetId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("TreeGraph.Api.Entities.CustomTableDefinition", "RefTableDefinition")
                        .WithMany()
                        .HasForeignKey("RefTableDefinitionId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("TreeGraph.Api.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.Navigation("RefCompositeType");

                    b.Navigation("RefOptionSet");

                    b.Navigation("RefTableDefinition");

                    b.Navigation("Unit");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.AttributeValue", b =>
                {
                    b.HasOne("TreeGraph.Api.Entities.AttributeDefinition", "Attribute")
                        .WithMany()
                        .HasForeignKey("AttributeId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("TreeGraph.Api.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.Navigation("Attribute");

                    b.Navigation("Unit");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CompositeFieldDefinition", b =>
                {
                    b.HasOne("TreeGraph.Api.Entities.CompositeTypeDefinition", "CompositeType")
                        .WithMany("Fields")
                        .HasForeignKey("CompositeTypeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("TreeGraph.Api.Entities.CompositeTypeDefinition", "RefCompositeType")
                        .WithMany()
                        .HasForeignKey("RefCompositeTypeId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("TreeGraph.Api.Entities.OptionSet", "RefOptionSet")
                        .WithMany()
                        .HasForeignKey("RefOptionSetId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("TreeGraph.Api.Entities.Unit", "Unit")
                        .WithMany()
                        .HasForeignKey("UnitId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.Navigation("CompositeType");

                    b.Navigation("RefCompositeType");

                    b.Navigation("RefOptionSet");

                    b.Navigation("Unit");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CustomTableColumn", b =>
                {
                    b.HasOne("TreeGraph.Api.Entities.CompositeTypeDefinition", "RefCompositeType")
                        .WithMany()
                        .HasForeignKey("RefCompositeTypeId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("TreeGraph.Api.Entities.CustomTableDefinition", "Table")
                        .WithMany("Columns")
                        .HasForeignKey("TableDefinitionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("RefCompositeType");

                    b.Navigation("Table");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CustomTableRow", b =>
                {
                    b.HasOne("TreeGraph.Api.Entities.AttributeDefinition", "Attribute")
                        .WithMany()
                        .HasForeignKey("AttributeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("TreeGraph.Api.Entities.CustomTableDefinition", "Table")
                        .WithMany()
                        .HasForeignKey("TableDefinitionId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Attribute");

                    b.Navigation("Table");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.OptionItem", b =>
                {
                    b.HasOne("TreeGraph.Api.Entities.OptionSet", "OptionSet")
                        .WithMany("Items")
                        .HasForeignKey("OptionSetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("OptionSet");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CompositeTypeDefinition", b =>
                {
                    b.Navigation("Fields");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.CustomTableDefinition", b =>
                {
                    b.Navigation("Columns");
                });

            modelBuilder.Entity("TreeGraph.Api.Entities.OptionSet", b =>
                {
                    b.Navigation("Items");
                });
#pragma warning restore 612, 618
        }
    }
}
```

## 文件 12/41 TreeGraph.Api/Data/Seeding/EavSeeder.cs

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;

namespace TreeGraph.Api.Data.Seeding;

/// <summary>
/// 示例元数据种子：组合类型 Specs + Brand、Product 属性目录、
/// 自定义表 certifications、选项集 gender / quality_grade。
///
/// 幂等保证：
///   - 严格的多表存在性检查（不是只查 AttributeCatalog）
///   - 整个 seed 包裹在事务中（含 execution strategy 重试兼容）
///   - 失败时事务回滚，不会留下部分数据
///
/// 依赖：UnitSeedService 已先行写入单位（net_weight 需要按符号查 kg 的 Id）。
/// </summary>
public static class EavSeeder
{
    public static async Task SeedAsync(EavDbContext db, CancellationToken ct = default)
    {
        // ── 严格的幂等检查：所有关键实体都存在才跳过 ──
        if (await IsAlreadySeededAsync(db, ct))
            return;

        // ── Npgsql 重试策略兼容：用 ExecutionStrategy 包裹整个 seed ──
        // （裸 BeginTransactionAsync 在 EnableRetryOnFailure 下会抛
        //   "execution strategy does not support user-initiated transactions"）
        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                await SeedInternalAsync(db, ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    /// <summary>
    /// 严格幂等检查：以下相关记录都存在才认为已 seed。
    /// 注意：同一 DbContext 不支持并发查询，必须顺序执行（不能 Task.WhenAll）。
    /// </summary>
    private static async Task<bool> IsAlreadySeededAsync(
        EavDbContext db, CancellationToken ct)
    {
        return await db.AttributeCatalog.AnyAsync(a => a.EntityType == "Product", ct)
            && await db.CompositeTypes.AnyAsync(
                t => t.EntityType == "Product" && t.TypeName == "Specs", ct)
            && await db.CompositeTypes.AnyAsync(
                t => t.EntityType == "Product" && t.TypeName == "Brand", ct)
            && await db.CustomTables.AnyAsync(
                t => t.EntityType == "Product" && t.TableName == "certifications", ct)
            && await db.OptionSets.AnyAsync(
                s => s.EntityType == "Shared" && s.SetName == "gender", ct)
            && await db.OptionSets.AnyAsync(
                s => s.EntityType == "Product" && s.SetName == "quality_grade", ct);
    }

    /// <summary>实际的 seed 逻辑（在事务中执行，中途 SaveChanges 不落库，Commit 才生效）</summary>
    private static async Task SeedInternalAsync(EavDbContext db, CancellationToken ct)
    {
        // ============================================================
        // 步骤 1：组合类型 Brand（先建，Specs 要引用它）
        // ============================================================
        var brand = new CompositeTypeDefinition
        {
            EntityType = "Product",
            TypeName = "Brand",
            DisplayName = "品牌",
            Fields =
            {
                new CompositeFieldDefinition
                {
                    FieldName = "name", DisplayName = "品牌名",
                    DataType = EavDataTypes.String, IsSearchable = true, DisplayOrder = 1
                },
                new CompositeFieldDefinition
                {
                    FieldName = "origin", DisplayName = "产地",
                    DataType = EavDataTypes.String, DisplayOrder = 2
                }
            }
        };
        db.CompositeTypes.Add(brand);
        await db.SaveChangesAsync(ct);
        // 此时 brand.CompositeTypeId 已生成

        // ============================================================
        // 步骤 2：组合类型 Specs（直接引用 brand，消除两阶段回填）
        // ============================================================
        var specs = new CompositeTypeDefinition
        {
            EntityType = "Product",
            TypeName = "Specs",
            DisplayName = "规格参数",
            Fields =
            {
                new CompositeFieldDefinition
                {
                    FieldName = "color", DisplayName = "颜色",
                    DataType = EavDataTypes.String, IsSearchable = true, DisplayOrder = 1
                },
                new CompositeFieldDefinition
                {
                    FieldName = "weight", DisplayName = "重量(kg)",
                    DataType = EavDataTypes.Decimal, DisplayOrder = 2
                },
                new CompositeFieldDefinition
                {
                    FieldName = "brand", DisplayName = "品牌信息",
                    DataType = EavDataTypes.Composite,
                    RefCompositeTypeId = brand.CompositeTypeId, // ← 直接引用
                    IsSearchable = true, DisplayOrder = 3
                }
            }
        };
        db.CompositeTypes.Add(specs);
        await db.SaveChangesAsync(ct);

        // ============================================================
        // 步骤 3：查 kg 单位（UnitSeedService 已先执行）
        // ============================================================
        var kgUnitId = await db.Units
            .Where(u => u.Category == "weight" && u.Symbol == "kg")
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (kgUnitId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "kg 单位未找到。请确认 UnitSeedService 已在 EavSeeder 之前执行。");
        }

        // ============================================================
        // 步骤 4：Product 属性目录（4 个基础属性 + 2 个引用属性）
        // ============================================================
        db.AttributeCatalog.AddRange(
            new AttributeDefinition
            {
                EntityType = "Product",
                AttributeName = "screen_size",
                DisplayName = "屏幕尺寸",
                DataType = EavDataTypes.Decimal,
                IsRequired = true,
                IsSearchable = true,
                IsSortable = true,
                DisplayOrder = 1,
                ValidationRule = JsonDocument.Parse("""{"min": 0, "max": 200}""")
            },
            new AttributeDefinition
            {
                EntityType = "Product",
                AttributeName = "release_date",
                DisplayName = "发布日期",
                DataType = EavDataTypes.Date,
                IsSearchable = true,
                DisplayOrder = 2
            },
            new AttributeDefinition
            {
                EntityType = "Product",
                AttributeName = "net_weight",
                DisplayName = "净重",
                DataType = EavDataTypes.Decimal,
                IsSearchable = true,
                IsSortable = true,
                DisplayOrder = 3,
                UnitId = kgUnitId, // 基准单位：千克
                ValidationRule = JsonDocument.Parse("""{"min": 0, "max": 10000}""")
            },
            new AttributeDefinition
            {
                EntityType = "Product",
                AttributeName = "specs",
                DisplayName = "规格参数",
                DataType = EavDataTypes.Composite,
                IsSearchable = true,
                DisplayOrder = 4,
                RefCompositeTypeId = specs.CompositeTypeId
            });

        // ============================================================
        // 步骤 5：自定义表 certifications
        // ============================================================
        var certs = new CustomTableDefinition
        {
            EntityType = "Product",
            TableName = "certifications",
            DisplayName = "认证证书",
            DisplayOrder = 1
        };
        certs.Columns.Add(new CustomTableColumn
        {
            ColumnName = "cert_name", DisplayName = "证书名称",
            DataType = EavDataTypes.String,
            IsRequired = true, IsSearchable = true, IsUnique = true, DisplayOrder = 1
        });
        certs.Columns.Add(new CustomTableColumn
        {
            ColumnName = "issuer", DisplayName = "颁发机构",
            DataType = EavDataTypes.String, IsSearchable = true, DisplayOrder = 2
        });
        certs.Columns.Add(new CustomTableColumn
        {
            ColumnName = "issued_date", DisplayName = "颁发日期",
            DataType = EavDataTypes.Date, DisplayOrder = 3
        });
        db.CustomTables.Add(certs);
        await db.SaveChangesAsync(ct);

        db.AttributeCatalog.Add(new AttributeDefinition
        {
            EntityType = "Product",
            AttributeName = "certifications",
            DisplayName = "认证证书",
            DataType = EavDataTypes.Table,
            IsSearchable = true,
            DisplayOrder = 5,
            RefTableDefinitionId = certs.TableDefinitionId
        });

        // ============================================================
        // 步骤 6：选项集 gender（Shared）+ quality_grade（Product）
        // ============================================================
        var gender = new OptionSet
        {
            EntityType = "Shared", SetName = "gender", DisplayName = "性别"
        };
        gender.Items.Add(new OptionItem { Value = "unknown", Label = "未知", DisplayOrder = 1, IsDefault = true });
        gender.Items.Add(new OptionItem { Value = "male", Label = "男", DisplayOrder = 2 });
        gender.Items.Add(new OptionItem { Value = "female", Label = "女", DisplayOrder = 3 });
        gender.Items.Add(new OptionItem { Value = "other", Label = "其他", DisplayOrder = 4 });
        db.OptionSets.Add(gender);

        var grade = new OptionSet
        {
            EntityType = "Product", SetName = "quality_grade", DisplayName = "质量等级"
        };
        grade.Items.Add(new OptionItem { Value = "grade_a", Label = "一级", DisplayOrder = 1 });
        grade.Items.Add(new OptionItem { Value = "grade_b", Label = "二级", DisplayOrder = 2 });
        grade.Items.Add(new OptionItem { Value = "grade_c", Label = "三级", DisplayOrder = 3 });
        db.OptionSets.Add(grade);
        await db.SaveChangesAsync(ct);

        db.AttributeCatalog.Add(new AttributeDefinition
        {
            EntityType = "Product",
            AttributeName = "quality_grade",
            DisplayName = "质量等级",
            DataType = EavDataTypes.SingleChoice,
            IsSearchable = true,
            DisplayOrder = 6,
            RefOptionSetId = grade.OptionSetId
        });

        // 最终一次性提交（之前每步 SaveChanges 已在事务内，最后一次统一落库）
        await db.SaveChangesAsync(ct);
    }
}
```

## 文件 13/41 TreeGraph.Api/Data/Seeding/UnitSeedService.cs

```csharp
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Entities;
using TreeGraph.Api.Services;

namespace TreeGraph.Api.Data.Seeding;

/// <summary>
/// 单位种子服务：固定 GUID，运行时一次性写入（幂等）。
///
/// ★ GUID 在本文件内显式指定（非随机），便于跨环境引用一致、
///   便于种子数据与外部系统对齐。
///
/// 排除的分类（有意为之）：
///   - 温度（°C / °F / K）：非线性换算（+273.15），
///     而 UnitConverter 只做乘法，纳入会导致数据损坏。
///   - 货币（CNY / USD / ...）：汇率动态，不能用固定 ToBaseFactor。
///
/// 注意：若数据库已存在旧单位（随机 GUID），本 seed 会因 AnyAsync()
/// 直接跳过，不会替换。切换到固定 GUID 需先清空 units 表：
///   DELETE FROM attribute_values WHERE unit_id IS NOT NULL;  -- 若被引用
///   DELETE FROM composite_field_definitions WHERE unit_id IS NOT NULL;
///   UPDATE attribute_catalog SET unit_id = NULL;
///   DELETE FROM units;
///   然后重启 API。
/// </summary>
public class UnitSeedService
{
    private readonly EavDbContext _db;
    private readonly IUnitCache _unitCache;
    private readonly ILogger<UnitSeedService> _logger;

    public UnitSeedService(
        EavDbContext db, IUnitCache unitCache, ILogger<UnitSeedService> logger)
    {
        _db = db;
        _unitCache = unitCache;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // 幂等：已有数据则跳过
        if (await _db.Units.AnyAsync(ct))
        {
            _logger.LogInformation("单位表已有数据，跳过 seed");
            return;
        }

        var units = BuildUnits();
        _db.Units.AddRange(units);
        await _db.SaveChangesAsync(ct);
        _unitCache.Invalidate();

        _logger.LogInformation("已 seed {Count} 个单位（固定 GUID）", units.Count);
    }

    /// <summary>
    /// 构建单位清单：13 个分类、50 个单位。GUID 显式固定。
    /// </summary>
    private static List<Unit> BuildUnits()
    {
        var units = new List<Unit>();
        var now = DateTimeOffset.UtcNow;

        // ==================== 长度 length（基准：米） ====================
        AddCategory(units, "length", baseSymbol: "m", now,
            ("c0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c", "米",     "m",   1.0m,          1),
            ("d1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", "千米",   "km",  1000m,         2),
            ("e2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", "厘米",   "cm",  0.01m,         3),
            ("f3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", "毫米",   "mm",  0.001m,        4),
            ("d7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", "英寸",   "in",  0.0254m,       5),
            ("c6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", "英尺",   "ft",  0.3048m,       6),
            ("b5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", "码",     "yd",  0.9144m,       7),
            ("a4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", "英里",   "mi",  1609.344m,     8)
        );

        // ==================== 重量 weight（基准：千克） ====================
        AddCategory(units, "weight", baseSymbol: "kg", now,
            ("e8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", "千克", "kg", 1.0m,             1),
            ("f9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f", "克",   "g",  0.001m,           2),
            ("a0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a", "毫克", "mg", 0.000001m,        3),
            ("b1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", "吨",   "t",  1000m,            4),
            ("c2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c", "磅",   "lb", 0.45359237m,      5),
            ("d3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d", "盎司", "oz", 0.028349523125m,  6)
        );

        // ==================== 体积 volume（基准：升） ====================
        AddCategory(units, "volume", baseSymbol: "L", now,
            ("2d21c35a-4251-479e-b814-060b2fc84445", "立方米", "m³", 1000m,  1),
            ("7f0af6a9-ba1a-469c-b967-e32afe43cad2", "升",     "L",  1.0m,   2),
            ("780e7a01-350d-45ec-b963-b36a996de614", "毫升",   "mL", 0.001m, 3)
        );

        // ==================== 面积 area（基准：平方米） ====================
        AddCategory(units, "area", baseSymbol: "m²", now,
            ("e88b04db-40ac-4bb7-b420-1f3b37180673", "平方米",   "m²", 1.0m,        1),
            ("0d7ebe17-93ae-4e4c-92f4-063a124cd181", "平方公里", "km²", 1000000m,  2),
            ("a4c312d3-023e-4d4e-b5a7-fb7fcbd55c56", "公顷",     "ha", 10000m,     3),
            ("fefa26a5-d608-411c-b637-469a886e558c", "亩",       "亩", 666.6666667m, 4)
        );

        // ==================== 时间 time（基准：秒） ====================
        AddCategory(units, "time", baseSymbol: "s", now,
            ("e4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e", "秒",   "s",   1.0m,    1),
            ("f5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", "分钟", "min", 60m,     2),
            ("a6e7f8a9-b0c1-4d2e-3f4a-5b6c7d8e9f0a", "小时", "h",   3600m,   3),
            ("b7f8a9b0-c1d2-4e3f-4a5b-6c7d8e9f0a1b", "天",   "d",   86400m,  4)
        );

        // ==================== 速度 speed（基准：米/秒） ====================
        AddCategory(units, "speed", baseSymbol: "m/s", now,
            ("4cbec89d-3f52-4db3-9ab0-faeeb841ffbf", "米/秒",      "m/s",  1.0m,          1),
            ("64e918fb-ee9d-45c7-b35a-2a55f5a5fe62", "千米/小时",  "km/h", 0.2777777778m, 2),
            ("405ae7a3-8a13-479d-bc1a-6f9d3c15e521", "英里/小时",  "mph",  0.44704m,      3)
        );

        // ==================== 角度 angle（基准：度） ====================
        AddCategory(units, "angle", baseSymbol: "°", now,
            ("ed1b66d2-454b-453b-9d43-12605dffa456", "度",   "°",   1.0m,              1),
            ("3d9088a3-7283-4f8f-b995-b193a57a6c2a", "弧度", "rad", 57.29577951308232m, 2)
        );

        // ==================== 电流 current（基准：安培） ====================
        AddCategory(units, "current", baseSymbol: "A", now,
            ("f1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f", "安培", "A",  1.0m,      1),
            ("a2e3f4a5-b6c7-4d8e-9f0a-1b2c3d4e5f6a", "毫安", "mA", 0.001m,    2),
            ("b3f4a5b6-c7d8-4e9f-0a1b-2c3d4e5f6a7b", "微安", "µA", 0.000001m, 3)
        );

        // ==================== 电压 voltage（基准：伏特） ====================
        AddCategory(units, "voltage", baseSymbol: "V", now,
            ("c4a5b6c7-d8e9-4f0a-1b2c-3d4e5f6a7b8c", "伏特", "V",  1.0m,   1),
            ("d5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d", "千伏", "kV", 1000m,  2),
            ("e6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", "毫伏", "mV", 0.001m, 3)
        );

        // ==================== 功率 power（基准：瓦特） ====================
        AddCategory(units, "power", baseSymbol: "W", now,
            ("f7d8e9f0-a1b2-4c3d-4e5f-6a7b8c9d0e1f", "瓦特", "W",  1.0m,              1),
            ("a8e9f0a1-b2c3-4d4e-5f6a-7b8c9d0e1f2a", "千瓦", "kW", 1000m,             2),
            ("b9f0a1b2-c3d4-4e5f-6a7b-8c9d0e1f2a3b", "兆瓦", "MW", 1000000m,          3),
            ("a00be966-be2e-484e-92b6-9706494ac775", "马力", "hp", 745.6998715822702m, 4)
        );

        // ==================== 压力 pressure（基准：帕斯卡） ====================
        AddCategory(units, "pressure", baseSymbol: "Pa", now,
            ("221d3c45-911f-4e94-9c3e-c13e6f2bcc76", "帕斯卡", "Pa",  1.0m,      1),
            ("883a9940-84ec-4daa-8448-609461b984ea", "千帕",   "kPa", 1000m,     2),
            ("d5306eb8-324f-4088-a867-6fbc7141fd59", "兆帕",   "MPa", 1000000m,  3),
            ("8981bd9a-bd99-4f4c-b8af-038975b799be", "巴",     "bar", 100000m,   4)
        );

        // ==================== 能量 energy（基准：焦耳） ====================
        AddCategory(units, "energy", baseSymbol: "J", now,
            ("5db494d7-2a9c-4ae0-86e0-d4bb0dfc7b81", "焦耳",   "J",   1.0m,       1),
            ("279a6b18-6d01-4437-b95b-0480ca7adc98", "千焦",   "kJ",  1000m,      2),
            ("e3c1f025-3b2b-461a-a5a1-015cb4e3fe38", "千瓦时", "kWh", 3600000m,   3)
        );

        // ==================== 频率 frequency（基准：赫兹） ====================
        AddCategory(units, "frequency", baseSymbol: "Hz", now,
            ("5e880060-9410-40d7-bcb4-545ccd0c1bb6", "赫兹", "Hz",  1.0m,      1),
            ("1a80ed3b-1b36-4d8b-b80b-3070dbc7979d", "千赫", "kHz", 1000m,     2),
            ("41572712-95dd-4caf-b316-e1b924bc57c3", "兆赫", "MHz", 1000000m,  3)
        );

        // ★ 有意排除：
        //   - 温度：非线性换算（°C ↔ K = ±273.15），UnitConverter 只做乘法
        //   - 货币：汇率动态，不能用固定 ToBaseFactor
        //
        // 若未来需要，应在扩展 UnitConverter 支持 affine 变换（offset + factor）
        // 后再纳入。参见：UnitsController / UnitConverter。

        return units;
    }

    /// <summary>
    /// 为一个分类添加单位。
    /// 基准单位（symbol == baseSymbol）自动标记 IsBaseUnit = true。
    /// </summary>
    private static void AddCategory(
        List<Unit> units, string category, string baseSymbol,
        DateTimeOffset now,
        params (string Id, string Name, string Symbol, decimal Factor, int Order)[] items)
    {
        foreach (var (id, name, symbol, factor, order) in items)
        {
            units.Add(new Unit
            {
                Id = Guid.Parse(id),
                Category = category,
                Name = name,
                Symbol = symbol,
                ToBaseFactor = factor,
                IsBaseUnit = symbol == baseSymbol,
                DisplayOrder = order,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }
}
```

## 文件 14/41 TreeGraph.Api/Entities/AttributeAuditLog.cs

```csharp
namespace TreeGraph.Api.Entities;

/// <summary>属性变更审计日志（与值变更同事务提交）</summary>
public class AttributeAuditLog
{
    public string AuditId { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string AttributeId { get; set; } = "";
    public string AttributeName { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    /// <summary>Insert / Update / Delete</summary>
    public string ChangeType { get; set; } = "";

    public string ChangedBy { get; set; } = "";
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; set; }
    public string? ClientIp { get; set; }
}
```

## 文件 15/41 TreeGraph.Api/Entities/AttributeDefinition.cs

```csharp
using System.Text.Json;

namespace TreeGraph.Api.Entities;

/// <summary>属性元数据（属性目录）</summary>
public class AttributeDefinition
{
    public string AttributeId { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string AttributeName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DataType { get; set; } = "string";

    public bool IsRequired { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsSortable { get; set; }
    public bool IsDeleted { get; set; }
    public int Version { get; set; } = 1;
    public int DisplayOrder { get; set; }

    /// <summary>枚举约束（JSON 数组），映射 jsonb</summary>
    public JsonDocument? AllowedValues { get; set; }

    /// <summary>验证规则（JSON 对象）</summary>
    public JsonDocument? ValidationRule { get; set; }

    public string? DefaultValue { get; set; }

    /// <summary>组合类型关联（当 DataType = "composite" 时）</summary>
    public string? RefCompositeTypeId { get; set; }
    public CompositeTypeDefinition? RefCompositeType { get; set; }

    /// <summary>表结构关联（当 DataType = "table" 时，行数据存 custom_table_rows）</summary>
    public string? RefTableDefinitionId { get; set; }
    public CustomTableDefinition? RefTableDefinition { get; set; }

    /// <summary>选项集关联（当 DataType = "single_choice" 时）</summary>
    public string? RefOptionSetId { get; set; }
    public OptionSet? RefOptionSet { get; set; }

    /// <summary>数值属性的基准单位（所有值归一化到此单位存储）</summary>
    public Guid? UnitId { get; set; }
    public Unit? Unit { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

## 文件 16/41 TreeGraph.Api/Entities/AttributeValue.cs

```csharp
using System.Text.Json;

namespace TreeGraph.Api.Entities;

/// <summary>类型化值表：每种基础类型独立存储列</summary>
public class AttributeValue
{
    public string ValueId { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string AttributeId { get; set; } = "";

    // 类型化值列
    public string? ValueString { get; set; }
    public long? ValueInt { get; set; }
    public decimal? ValueDecimal { get; set; }
    public bool? ValueBool { get; set; }
    public DateTimeOffset? ValueDatetime { get; set; }
    public DateOnly? ValueDateOnly { get; set; }
    public TimeOnly? ValueTime { get; set; }
    public JsonDocument? ValueFileMeta { get; set; }
    public JsonDocument? ValueJsonb { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>原始输入单位（仅用于展示还原，不参与查询比较）</summary>
    public Guid? UnitId { get; set; }
    public Unit? Unit { get; set; }

    public AttributeDefinition Attribute { get; set; } = null!;
}
```

## 文件 17/41 TreeGraph.Api/Entities/CompositeTypeDefinition.cs

```csharp
namespace TreeGraph.Api.Entities;

/// <summary>组合类型定义</summary>
public class CompositeTypeDefinition
{
    public string CompositeTypeId { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<CompositeFieldDefinition> Fields { get; set; } = new();
}

/// <summary>
/// 组合类型字段定义。
///
/// ★ #8：新增 RefOptionSetId，组合内 single_choice 可用选项集（与属性级对齐）。
/// 与 AllowedValues 并存：优先使用 RefOptionSetId，未设置时回退到 AllowedValues。
/// 数据库 CHECK 约束：仅 single_choice 类型可以设置 ref_option_set_id。
/// </summary>
public class CompositeFieldDefinition
{
    public string FieldId { get; set; } = "";
    public string CompositeTypeId { get; set; } = "";
    public string FieldName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DataType { get; set; } = "string";

    /// <summary>当 DataType = "composite" 时指向嵌套类型</summary>
    public string? RefCompositeTypeId { get; set; }
    public CompositeTypeDefinition? RefCompositeType { get; set; }

    /// <summary>数量字段的基准单位（仅 DataType = "decimal" 允许）</summary>
    public Guid? UnitId { get; set; }
    public Unit? Unit { get; set; }

    /// <summary>
    /// ★ #8：选项集引用（仅 DataType = "single_choice" 允许）。
    /// 设置后，选项集提供的 Value / Label 优先于 AllowedValues。
    /// </summary>
    public string? RefOptionSetId { get; set; }
    public OptionSet? RefOptionSet { get; set; }

    public bool IsArray { get; set; }
    public bool IsRequired { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsSortable { get; set; }
    public bool IsDeleted { get; set; }
    public int DisplayOrder { get; set; }

    public System.Text.Json.JsonDocument? ValidationRule { get; set; }
    public System.Text.Json.JsonDocument? AllowedValues { get; set; }
    public string? DefaultValue { get; set; }

    public CompositeTypeDefinition CompositeType { get; set; } = null!;
}
```

## 文件 18/41 TreeGraph.Api/Entities/CustomTableDefinition.cs

```csharp
using System.Text.Json;

namespace TreeGraph.Api.Entities;

/// <summary>自定义表结构定义（1:N，多行结构；列结构由 CustomTableColumn 定义）</summary>
public class CustomTableDefinition
{
    public string TableDefinitionId { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string TableName { get; set; } = "";       // "certifications"、"education"
    public string DisplayName { get; set; } = "";     // "认证证书"、"教育经历"
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<CustomTableColumn> Columns { get; set; } = new();
}

/// <summary>自定义表列定义（任意基础类型或 composite）</summary>
public class CustomTableColumn
{
    public string ColumnId { get; set; } = "";
    public string TableDefinitionId { get; set; } = "";
    public string ColumnName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DataType { get; set; } = "string";

    /// <summary>当 DataType = "composite" 时指向嵌套组合类型</summary>
    public string? RefCompositeTypeId { get; set; }
    public CompositeTypeDefinition? RefCompositeType { get; set; }

    public bool IsRequired { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsSortable { get; set; }
    public bool IsUnique { get; set; }                // 列内唯一（如"证书名"不重复）
    public bool IsDeleted { get; set; }
    public int DisplayOrder { get; set; }
    public JsonDocument? ValidationRule { get; set; }
    public JsonDocument? AllowedValues { get; set; }
    public string? DefaultValue { get; set; }

    public CustomTableDefinition Table { get; set; } = null!;
}
```

## 文件 19/41 TreeGraph.Api/Entities/CustomTableRow.cs

```csharp
using System.Text.Json;

namespace TreeGraph.Api.Entities;

/// <summary>
/// 自定义表行数据：所有表共享一张物理行表，按 TableDefinitionId 区分，
/// 行内结构由列定义驱动，数据存 JSONB
/// </summary>
public class CustomTableRow
{
    public string RowId { get; set; } = "";
    public string TableDefinitionId { get; set; } = "";
    public string AttributeId { get; set; } = "";     // 关联的属性（一个属性对应一张表）
    public string ParentEntityId { get; set; } = "";
    public string ParentEntityType { get; set; } = "";

    /// <summary>行内数据 JSONB：{ "cert_name": "CE", "issuer": "TÜV" }</summary>
    public JsonDocument RowData { get; set; } = null!;

    public int RowOrder { get; set; }                 // 行的展示顺序
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AttributeDefinition Attribute { get; set; } = null!;
    public CustomTableDefinition Table { get; set; } = null!;
}
```

## 文件 20/41 TreeGraph.Api/Entities/OptionSet.cs

```csharp
namespace TreeGraph.Api.Entities;

/// <summary>选项集（一组互斥的单选值，可跨实体类型共享）</summary>
public class OptionSet
{
    public string OptionSetId { get; set; } = "";
    public string EntityType { get; set; } = "";      // "Shared" 表示全局共享
    public string SetName { get; set; } = "";         // "gender"、"status"
    public string DisplayName { get; set; } = "";     // "性别"
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// ★ 软删除标记。
    /// 后端读取端（OptionSetCache.GetSet）**不按此过滤**：
    /// 历史数据的 Value 仍需通过集合拿 Label。
    /// 仅管理页列表 / GetAll 过滤此标记。
    /// </summary>
    public bool IsDeleted { get; set; }

    public List<OptionItem> Items { get; set; } = new();
}

/// <summary>选项项：Value 是存储的实际值，Label 是展示名</summary>
public class OptionItem
{
    public string OptionItemId { get; set; } = "";
    public string OptionSetId { get; set; } = "";
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public int DisplayOrder { get; set; }

    /// <summary>是否默认选项（同集内业务代码保证只有一个 true）</summary>
    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>软删除（历史数据仍存有旧 Value，删除后读取端降级显示 Value）</summary>
    public bool IsDeleted { get; set; }

    public OptionSet OptionSet { get; set; } = null!;
}
```

## 文件 21/41 TreeGraph.Api/Entities/Unit.cs

```csharp
namespace TreeGraph.Api.Entities;

/// <summary>计量单位（按分类组织，每个分类一个基准单位）</summary>
public class Unit
{
    public Guid Id { get; set; }

    /// <summary>分类：length / weight / volume ...</summary>
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";

    /// <summary>换算到分类基准单位的系数</summary>
    public decimal ToBaseFactor { get; set; }

    /// <summary>是否为该分类的基准单位</summary>
    public bool IsBaseUnit { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

## 文件 22/41 TreeGraph.Api/Infrastructure/DbExceptionHandler.cs

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TreeGraph.Api.Infrastructure;

/// <summary>把 EF Core / Npgsql 的已知异常映射为合适的 HTTP 响应</summary>
public sealed class DbExceptionHandler : IExceptionHandler
{
    private readonly ILogger<DbExceptionHandler> _logger;

    public DbExceptionHandler(ILogger<DbExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception ex, CancellationToken ct)
    {
        // ★ 修复：ExecuteUpdateAsync / ExecuteDeleteAsync 会抛出未经 DbUpdateException
        //   包装的裸 PostgresException，需要单独处理，否则会 500。
        var pg = ex switch
        {
            DbUpdateException due when due.InnerException is PostgresException inner => inner,
            PostgresException direct => direct,
            _ => null
        };

        if (pg is null) return false;

        if (pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            _logger.LogWarning("唯一约束冲突: {Constraint}", pg.ConstraintName);
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "资源已存在",
                constraint = pg.ConstraintName
            }, ct);
            return true;
        }

        if (pg.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            _logger.LogWarning("外键约束冲突: {Constraint}", pg.ConstraintName);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "外键约束冲突",
                constraint = pg.ConstraintName
            }, ct);
            return true;
        }

        // ★ 新增：CHECK 约束（如 ck_attr_int_no_unit / ck_composite_field_*）
        if (pg.SqlState == PostgresErrorCodes.CheckViolation)
        {
            _logger.LogWarning("CHECK 约束冲突: {Constraint}", pg.ConstraintName);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "约束校验失败",
                constraint = pg.ConstraintName
            }, ct);
            return true;
        }

        return false;
    }
}
```

## 文件 23/41 TreeGraph.Api/Program.cs

```csharp
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Data.Seeding;
using TreeGraph.Api.Infrastructure;
using TreeGraph.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ★ Aspire ServiceDefaults：服务发现、健康检查、OpenTelemetry
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new NumericValueJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new DynamicCompositeValueJsonConverter());
    });
builder.Services.AddOpenApi();

// ★ 全局异常处理：把 EF/Npgsql 已知异常映射为 409/400
builder.Services.AddExceptionHandler<DbExceptionHandler>();
builder.Services.AddProblemDetails();

// .NET Aspire 集成:自动从 ConnectionStrings:TreeGraphDb 注入连接字符串
builder.AddNpgsqlDbContext<EavDbContext>("TreeGraphDb");

builder.Services.AddMemoryCache();
builder.Services.AddScoped<EavWriteService>();
builder.Services.AddScoped<EavReadService>();
builder.Services.AddScoped<EavQueryService>();
builder.Services.AddScoped<EavValidationService>();
builder.Services.AddScoped<CompositeValueService>();
builder.Services.AddSingleton<IAttributeCache, AttributeCache>();
builder.Services.AddSingleton<ICompositeTypeCache, CompositeTypeCache>();
builder.Services.AddSingleton<IUnitCache, UnitCache>();
builder.Services.AddSingleton<UnitConverter>();
builder.Services.AddSingleton<ICustomTableCache, CustomTableCache>();
builder.Services.AddSingleton<IOptionSetCache, OptionSetCache>();
builder.Services.AddScoped<UnitSeedService>();
builder.Services.AddScoped<CustomTableValidationService>();
builder.Services.AddScoped<CustomTableWriteService>();
builder.Services.AddScoped<CustomTableReadService>();
builder.Services.AddScoped<CustomTableQueryService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ★ 研发阶段：暂不启用身份验证/授权管道
//   UseAuthorization() 会解析 IAuthorizationPolicyProvider，
//   未 AddAuthorization() 时首次请求抛 InvalidOperationException。
// app.UseAuthorization();

// ★ 全局异常处理
app.UseExceptionHandler();

app.MapControllers();

// ★ Aspire 默认端点（/health、/alive），供 Dashboard 探测
app.MapDefaultEndpoints();

// 启动时应用迁移并注入种子数据（EF 设计期跳过，避免 dotnet-ef 连接数据库）
if (!EF.IsDesignTime)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<UnitSeedService>().SeedAsync();
    await EavSeeder.SeedAsync(db);
}

app.Run();

// ★ 让 WebApplicationFactory<Program> 可引用（集成测试）
public partial class Program { }
```

## 文件 24/41 TreeGraph.Api/Services/AttributeCache.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;

namespace TreeGraph.Api.Services;

public interface IAttributeCache
{
    IReadOnlyList<AttributeDefinition> GetDefinitions(string entityType);
    AttributeDefinition? GetDefinition(string entityType, string attributeName);
    void Invalidate(string entityType);
}

/// <summary>属性元数据缓存（单例，读取频率极高）</summary>
public class AttributeCache : IAttributeCache
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public AttributeCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public IReadOnlyList<AttributeDefinition> GetDefinitions(string entityType)
    {
        return _cache.GetOrCreate($"eav:defs:{entityType}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
            return db.AttributeCatalog
                .Where(a => a.EntityType == entityType && !a.IsDeleted)
                .OrderBy(a => a.DisplayOrder)
                .AsNoTracking()
                .ToList();
        })!;
    }

    public AttributeDefinition? GetDefinition(string entityType, string attributeName)
        => GetDefinitions(entityType).FirstOrDefault(d => d.AttributeName == attributeName);

    public void Invalidate(string entityType)
        => _cache.Remove($"eav:defs:{entityType}");
}
```

## 文件 25/41 TreeGraph.Api/Services/CompositeTypeCache.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;

namespace TreeGraph.Api.Services;

public interface ICompositeTypeCache
{
    CompositeTypeDefinition GetType(string compositeTypeId);
    void Invalidate(string compositeTypeId);
}

/// <summary>组合类型定义缓存（单例）</summary>
public class CompositeTypeCache : ICompositeTypeCache
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public CompositeTypeCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public CompositeTypeDefinition GetType(string compositeTypeId)
    {
        return _cache.GetOrCreate($"eav:composite:{compositeTypeId}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
            return db.CompositeTypes
                .Include(t => t.Fields)
                    .ThenInclude(f => f.RefOptionSet)   // ★ #8
                .AsNoTracking()
                .First(t => t.CompositeTypeId == compositeTypeId);
        })!;
    }

    public void Invalidate(string compositeTypeId)
        => _cache.Remove($"eav:composite:{compositeTypeId}");
}
```

## 文件 26/41 TreeGraph.Api/Services/CompositeValueService.cs

```csharp
using System.Text.Json;
using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Services;

/// <summary>组合类型值服务：递归验证 / JSONB 序列化 / 反序列化</summary>
public class CompositeValueService
{
    private readonly ICompositeTypeCache _cache;
    private readonly EavValidationService _validator;

    /// <summary>
    /// ★ #4：注入单位服务以支持组合内 decimal 字段的单位换算。
    /// UnitId 存在于 CompositeFieldDefinition 上（数据库 CHECK 约束：
    /// 仅 decimal 类型可以绑定）。
    /// </summary>
    private readonly IUnitCache _unitCache;
    private readonly UnitConverter _converter;

    public CompositeValueService(
        ICompositeTypeCache cache,
        EavValidationService validator,
        IUnitCache unitCache,
        UnitConverter converter)
    {
        _cache = cache;
        _validator = validator;
        _unitCache = unitCache;
        _converter = converter;
    }

    // ============================================================
    // 验证
    // ============================================================

    public ValidationResult Validate(DynamicCompositeValue value, string compositeTypeId)
    {
        var typeDef = _cache.GetType(compositeTypeId);
        var errors = new List<ValidationError>();

        foreach (var field in typeDef.Fields.Where(f => !f.IsDeleted))
        {
            value.TryGet<object?>(field.FieldName, out var raw);

            if (field.IsRequired && raw is null)
            {
                errors.Add(new(field.FieldName, "必填字段"));
                continue;
            }
            if (raw is null) continue;

            if (field.IsArray)
            {
                if (raw is not IEnumerable<object?> items)
                {
                    errors.Add(new(field.FieldName, "期望数组"));
                    continue;
                }
                int i = 0;
                foreach (var item in items)
                {
                    ValidateField(field, item, $"{field.FieldName}[{i}]", errors);
                    i++;
                }
            }
            else
            {
                ValidateField(field, raw, field.FieldName, errors);
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    private void ValidateField(
        CompositeFieldDefinition field, object? value,
        string path, List<ValidationError> errors)
    {
        if (field.DataType == EavDataTypes.Composite)
        {
            if (value is not DynamicCompositeValue nested)
            {
                errors.Add(new(path, "期望嵌套组合值"));
                return;
            }
            var nestedResult = Validate(nested, field.RefCompositeTypeId!);
            foreach (var e in nestedResult.Errors)
                errors.Add(new($"{path}.{e.Field}", e.Message));
            return;
        }

        // ★ #4：组合内 decimal 带单位时，校验单位分类与归一化后的范围
        if (field.DataType == EavDataTypes.Decimal && value is NumericValue nv)
        {
            if (field.UnitId is { } baseUnitId && nv.UnitId is { } inputUnitId)
            {
                try
                {
                    var baseUnit = _unitCache.Get(baseUnitId);
                    var inputUnit = _unitCache.Get(inputUnitId);
                    if (inputUnit.Category != baseUnit.Category)
                    {
                        errors.Add(new(path,
                            $"单位分类不匹配：期望 {baseUnit.Category}，实际 {inputUnit.Category}"));
                    }
                }
                catch (InvalidOperationException)
                {
                    errors.Add(new(path, "单位不存在"));
                }
            }
            else if (field.UnitId is null && nv.UnitId is not null)
            {
                errors.Add(new(path, "该字段未绑定基准单位，不允许指定单位"));
            }
            // 用归一化后的值做范围校验
            if (field.ValidationRule is not null)
            {
                var normalized = NormalizeToBase(field, nv.Value, nv.UnitId);
                var result = ValidateNumericRange(field, normalized);
                if (result is not null)
                    errors.Add(new(path, result));
            }
            return;
        }

        // 其它类型：构造临时 AttributeDefinition 复用基础验证器
        var tempDef = new AttributeDefinition
        {
            AttributeName = path,
            DataType = field.DataType,
            IsRequired = field.IsRequired,
            ValidationRule = field.ValidationRule,
            AllowedValues = field.AllowedValues
        };
        var result2 = _validator.ValidateBaseValue(tempDef, value);
        errors.AddRange(result2.Errors);
    }

    private decimal NormalizeToBase(
        CompositeFieldDefinition field, decimal value, Guid? inputUnitId)
    {
        if (field.UnitId is not { } baseUnitId) return value;
        if (inputUnitId is null || inputUnitId == baseUnitId) return value;
        return _converter.ToBase(value, inputUnitId.Value, baseUnitId);
    }

    private static string? ValidateNumericRange(
        CompositeFieldDefinition field, decimal value)
    {
        if (field.ValidationRule is null) return null;
        var rule = field.ValidationRule.RootElement;

        if (rule.TryGetProperty("min", out var min)
            && min.TryGetDecimal(out var minVal)
            && value < minVal)
            return $"不能小于 {minVal}";

        if (rule.TryGetProperty("max", out var max)
            && max.TryGetDecimal(out var maxVal)
            && value > maxVal)
            return $"不能大于 {maxVal}";

        return null;
    }

    // ============================================================
    // 序列化
    // ============================================================

    /// <summary>
    /// 序列化为 JsonDocument（用于存储 ValueJsonb）。
    ///
    /// ★ #4：组合内 decimal 字段带单位时，序列化为
    /// `{ value: <归一化到基准单位的数值>, unitId: <原始输入单位 Guid> }`。
    /// 读取端（Deserialize）根据 originalUnits 决定是否还原。
    /// </summary>
    public JsonDocument Serialize(DynamicCompositeValue value, string compositeTypeId)
    {
        var typeDef = _cache.GetType(compositeTypeId);
        var dict = new Dictionary<string, object?>();

        foreach (var field in typeDef.Fields.Where(f => !f.IsDeleted))
        {
            value.TryGet<object?>(field.FieldName, out var raw);
            dict[field.FieldName] = SerializeFieldValue(raw, field);
        }

        return JsonDocument.Parse(JsonSerializer.Serialize(dict));
    }

    private object? SerializeFieldValue(object? value, CompositeFieldDefinition field)
    {
        if (value is null) return null;

        if (field.IsArray && value is IEnumerable<object?> items)
            return items.Select(i => SerializeFieldValue(i, field)).ToList();

        if (field.DataType == EavDataTypes.Composite && value is DynamicCompositeValue nested)
        {
            var inner = Serialize(nested, field.RefCompositeTypeId!);
            return inner.RootElement.Clone();
        }

        // ★ #4：组合内 decimal 的 NumericValue 序列化
        if (field.DataType == EavDataTypes.Decimal && value is NumericValue nv)
        {
            if (field.UnitId is not { } baseUnitId || nv.UnitId is null)
                return nv.Value;  // 无单位：存裸数值

            var baseValue = _converter.ToBase(nv.Value, nv.UnitId.Value, baseUnitId);
            return new { value = baseValue, unitId = nv.UnitId.Value };
        }

        // 基础类型：按类型返回可序列化对象
        return field.DataType switch
        {
            EavDataTypes.Datetime when value is DateTimeOffset dto => dto,
            EavDataTypes.Date when value is DateOnly d => d.ToString("yyyy-MM-dd"),
            EavDataTypes.Time when value is TimeOnly t => t.ToString("HH:mm:ss"),
            _ => value
        };
    }

    // ============================================================
    // 反序列化
    // ============================================================

    /// <summary>
    /// 从 JsonDocument 反序列化为运行时值。
    ///
    /// ★ #4：新增 originalUnits 参数。
    ///   - false（默认）：组合内 decimal 返回基准单位值
    ///   - true：按原始输入单位还原
    /// </summary>
    public DynamicCompositeValue Deserialize(
        JsonDocument doc, string compositeTypeId, bool originalUnits = false)
    {
        var typeDef = _cache.GetType(compositeTypeId);
        var result = new DynamicCompositeValue(typeDef.TypeName);
        var root = doc.RootElement;

        foreach (var field in typeDef.Fields.Where(f => !f.IsDeleted))
        {
            if (!root.TryGetProperty(field.FieldName, out var elem)) continue;
            result[field.FieldName] = DeserializeFieldValue(elem, field, originalUnits);
        }

        return result;
    }

    private object? DeserializeFieldValue(
        JsonElement elem, CompositeFieldDefinition field, bool originalUnits)
    {
        if (elem.ValueKind == JsonValueKind.Null) return null;

        if (field.IsArray)
            return elem.EnumerateArray()
                .Select(e => DeserializeFieldValue(e, field, originalUnits)).ToList();

        if (field.DataType == EavDataTypes.Composite)
        {
            using var nestedDoc = JsonDocument.Parse(elem.GetRawText());
            return Deserialize(nestedDoc, field.RefCompositeTypeId!, originalUnits);
        }

        // ★ #4：组合内 decimal 的 {value, unitId} 对象
        if (field.DataType == EavDataTypes.Decimal && elem.ValueKind == JsonValueKind.Object)
        {
            var baseValue = elem.GetProperty("value").GetDecimal();
            Guid? originalUnitId = elem.TryGetProperty("unitId", out var u)
                                    && u.ValueKind == JsonValueKind.String
                ? Guid.Parse(u.GetString()!)
                : null;

            if (field.UnitId is not { } baseUnitId)
                return new NumericValue(baseValue, null);

            if (!originalUnits || originalUnitId is null || originalUnitId == baseUnitId)
                return new NumericValue(baseValue, baseUnitId);

            try
            {
                var restored = _converter.FromBase(baseValue, baseUnitId, originalUnitId.Value);
                return new NumericValue(restored, originalUnitId);
            }
            catch (InvalidOperationException)
            {
                return new NumericValue(baseValue, baseUnitId);
            }
        }

        // ★ 修复 D1：decimal 字段绑定了基准单位时，裸数字（旧数据形态）也返回
        //   NumericValue，与 Object 分支（{ value, unitId }）保持类型一致；
        //   未绑定单位时仍返回裸 decimal。when 子句必须在裸 Decimal 分支之前。
        return field.DataType switch
        {
            EavDataTypes.Int => elem.GetInt64(),
            EavDataTypes.Decimal when field.UnitId is { } baseUnitId
                => new NumericValue(elem.GetDecimal(), baseUnitId),
            EavDataTypes.Decimal => elem.GetDecimal(),
            EavDataTypes.Bool => elem.GetBoolean(),
            EavDataTypes.Datetime => elem.GetDateTimeOffset(),
            EavDataTypes.Date => DateOnly.Parse(elem.GetString()!),
            EavDataTypes.Time => TimeOnly.Parse(elem.GetString()!),
            _ => elem.GetString()
        };
    }
}
```

## 文件 27/41 TreeGraph.Api/Services/CustomTableCache.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;

namespace TreeGraph.Api.Services;

public interface ICustomTableCache
{
    CustomTableDefinition GetTable(string tableDefinitionId);
    CustomTableDefinition? GetTableByName(string entityType, string tableName);
    void Invalidate(string tableDefinitionId);

    /// <summary>
    /// 清除按名称索引的缓存（name → id 映射）。
    /// 表创建/软删除/改版本后，若希望立即生效可调用；否则等 TTL 到期。
    /// </summary>
    void InvalidateByName(string entityType, string tableName);
}

/// <summary>
/// 自定义表结构缓存（单例，含列定义，过滤软删除列）。
///
/// 两级缓存：
///   - 实体缓存：key = `eav:ctable:{id}`       → 完整 CustomTableDefinition（含 Columns）
///   - 名称索引：key = `eav:ctable:name:{et}:{tn}` → TableDefinitionId（空字符串表示不存在）
///
/// GetTableByName 命中两级缓存时，**不创建 DI scope**，避免高频调用时
/// 每次都查 DB（即使命中实体缓存也会先创建 scope 查 id）。
/// </summary>
public class CustomTableCache : ICustomTableCache
{
    private const string EntityKeyPrefix = "eav:ctable:";
    private const string NameKeyPrefix = "eav:ctable:name:";

    private static readonly TimeSpan EntityTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan NameTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Sliding = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public CustomTableCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    private static string EntityKey(string id) => $"{EntityKeyPrefix}{id}";
    private static string NameKey(string entityType, string tableName)
        => $"{NameKeyPrefix}{entityType}:{tableName}";

    public CustomTableDefinition GetTable(string tableDefinitionId)
    {
        return _cache.GetOrCreate(EntityKey(tableDefinitionId), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = EntityTtl;
            entry.SlidingExpiration = Sliding;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
            var table = db.CustomTables
                .Include(t => t.Columns)
                .AsNoTracking()
                // ★ 过滤软删除：防止 name 缓存命中旧 id 时把已删表重新加载回实体缓存
                .First(t => t.TableDefinitionId == tableDefinitionId && !t.IsDeleted);
            table.Columns.RemoveAll(c => c.IsDeleted);
            return table;
        })!;
    }

    /// <summary>
    /// 按 (entityType, tableName) 查询表定义。
    ///
    /// ★ P2-2：name → id 映射也缓存（TTL 10 分钟），命中时直接走实体缓存，
    ///   不再每次创建 DI scope 查 DB。
    ///
    /// 最终一致：表被软删除/新版本上线后，最多 10 分钟内按名查询仍返回旧值。
    /// 需要立即生效，请调用 <see cref="InvalidateByName"/>。
    /// </summary>
    public CustomTableDefinition? GetTableByName(string entityType, string tableName)
    {
        // 空字符串是哨兵值（TableDefinitionId 是 GUID 字符串，不会为空），
        // 表示"不存在"。用它避免 IMemoryCache 不缓存 null 导致的缓存击穿。
        var id = _cache.GetOrCreate(NameKey(entityType, tableName), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = NameTtl;
            entry.SlidingExpiration = Sliding;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
            return db.CustomTables
                .Where(t => t.EntityType == entityType
                         && t.TableName == tableName
                         && !t.IsDeleted)
                .OrderByDescending(t => t.Version)
                .Select(t => t.TableDefinitionId)
                .FirstOrDefault();   // 无记录时返回空字符串
        });

        return id is null || id.Length == 0 ? null : GetTable(id);
    }

    public void Invalidate(string tableDefinitionId)
        => _cache.Remove(EntityKey(tableDefinitionId));

    public void InvalidateByName(string entityType, string tableName)
        => _cache.Remove(NameKey(entityType, tableName));
}
```

## 文件 28/41 TreeGraph.Api/Services/CustomTableQueryService.cs

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;

namespace TreeGraph.Api.Services;

/// <summary>自定义表行内字段查询：JSONB @> 包含查询（走 ix_ctr_rowdata GIN 索引）</summary>
public class CustomTableQueryService
{
    private readonly EavDbContext _db;
    private readonly ICustomTableCache _tableCache;

    public CustomTableQueryService(EavDbContext db, ICustomTableCache tableCache)
    {
        _db = db;
        _tableCache = tableCache;
    }

    /// <summary>
    /// 查找所有含指定行数据的父实体
    /// 例：查找含 cert_name=CE 的证书的所有产品
    /// </summary>
    public async Task<List<string>> FindParentEntitiesByRowAsync(
        string parentEntityType, string attributeId,
        Dictionary<string, object?> rowFilter,
        CancellationToken ct = default)
    {
        // JsonDocument 参数保证 Npgsql 将条件翻译为 jsonb @> jsonb
        // （不 using：查询延迟执行，参数值在物化时才读取）
        var filterDoc = JsonDocument.Parse(JsonSerializer.Serialize(rowFilter));

        return await _db.CustomTableRows
            .Where(r => r.ParentEntityType == parentEntityType
                     && r.AttributeId == attributeId
                     && EF.Functions.JsonContains(r.RowData, filterDoc))
            .Select(r => r.ParentEntityId)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>
    /// 组合多条件查询：每个条件匹配某一行，多条件间取交集（AND 语义）
    /// </summary>
    public async Task<List<string>> FindByMultipleRowConditionsAsync(
        string parentEntityType, string attributeId,
        List<Dictionary<string, object?>> rowConditions,
        CancellationToken ct = default)
    {
        IQueryable<string>? result = null;

        foreach (var condition in rowConditions)
        {
            var filterDoc = JsonDocument.Parse(JsonSerializer.Serialize(condition));
            var filter = filterDoc;

            var sub = _db.CustomTableRows
                .Where(r => r.ParentEntityType == parentEntityType
                         && r.AttributeId == attributeId
                         && EF.Functions.JsonContains(r.RowData, filter))
                .Select(r => r.ParentEntityId)
                .Distinct();

            result = result is null ? sub : result.Intersect(sub);
        }

        return result is null
            ? new List<string>()
            : await result.ToListAsync(ct);
    }
}
```

## 文件 29/41 TreeGraph.Api/Services/CustomTableReadService.cs

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Services;

/// <summary>自定义表读取服务：按父实体 + 属性加载整表</summary>
public class CustomTableReadService
{
    private readonly EavDbContext _db;
    private readonly ICustomTableCache _tableCache;
    private readonly CompositeValueService _composite;

    public CustomTableReadService(
        EavDbContext db,
        ICustomTableCache tableCache,
        CompositeValueService composite)
    {
        _db = db;
        _tableCache = tableCache;
        _composite = composite;
    }

    public async Task<CustomTableValue> LoadAsync(
        string parentEntityId, string parentEntityType,
        string attributeId, string tableDefinitionId,
        CancellationToken ct = default)
    {
        var table = _tableCache.GetTable(tableDefinitionId);
        var rows = await _db.CustomTableRows
            .Where(r => r.ParentEntityType == parentEntityType
                     && r.ParentEntityId == parentEntityId
                     && r.AttributeId == attributeId)
            .OrderBy(r => r.RowOrder)
            .AsNoTracking()
            .ToListAsync(ct);

        return new CustomTableValue
        {
            TableName = table.TableName,
            Rows = rows.Select(r => DeserializeRow(r, table)).ToList()
        };
    }

    private CustomTableRowValue DeserializeRow(
        CustomTableRow row, CustomTableDefinition table)
    {
        var result = new CustomTableRowValue
        {
            RowId = row.RowId,
            RowOrder = row.RowOrder
        };
        var root = row.RowData.RootElement;

        foreach (var col in table.Columns)
        {
            if (!root.TryGetProperty(col.ColumnName, out var elem)) continue;
            result.Fields[col.ColumnName] = DeserializeFieldValue(elem, col);
        }
        return result;
    }

    private object? DeserializeFieldValue(JsonElement elem, CustomTableColumn col)
    {
        if (elem.ValueKind == JsonValueKind.Null) return null;

        if (col.DataType == EavDataTypes.Composite)
        {
            using var doc = JsonDocument.Parse(elem.GetRawText());
            return _composite.Deserialize(doc, col.RefCompositeTypeId!);
        }

        return col.DataType switch
        {
            EavDataTypes.Int => elem.GetInt64(),
            EavDataTypes.Decimal => elem.GetDecimal(),
            EavDataTypes.Bool => elem.GetBoolean(),
            EavDataTypes.Datetime => elem.GetDateTimeOffset(),
            EavDataTypes.Date => DateOnly.Parse(elem.GetString()!),
            EavDataTypes.Time => TimeOnly.Parse(elem.GetString()!),
            _ => elem.GetString()
        };
    }
}
```

## 文件 30/41 TreeGraph.Api/Services/CustomTableValidationService.cs

```csharp
using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Services;

/// <summary>
/// 自定义表验证引擎：逐行按列定义验证，复用基础类型与组合类型验证器，本身不实现新的验证逻辑
/// </summary>
public class CustomTableValidationService
{
    private readonly ICustomTableCache _tableCache;
    private readonly EavValidationService _baseValidator;
    private readonly CompositeValueService _compositeValidator;

    public CustomTableValidationService(
        ICustomTableCache tableCache,
        EavValidationService baseValidator,
        CompositeValueService compositeValidator)
    {
        _tableCache = tableCache;
        _baseValidator = baseValidator;
        _compositeValidator = compositeValidator;
    }

    public ValidationResult Validate(CustomTableValue value, string tableDefinitionId)
    {
        var table = _tableCache.GetTable(tableDefinitionId);
        var errors = new List<ValidationError>();

        // 行内列唯一性校验的缓存
        var uniqueColumnValues = new Dictionary<string, HashSet<string>>();

        for (int rowIdx = 0; rowIdx < value.Rows.Count; rowIdx++)
        {
            var row = value.Rows[rowIdx];
            var rowPath = $"[{rowIdx}]";

            foreach (var col in table.Columns.OrderBy(c => c.DisplayOrder))
            {
                row.Fields.TryGetValue(col.ColumnName, out var rawValue);

                // 必填
                if (col.IsRequired && rawValue is null)
                {
                    errors.Add(new($"{rowPath}.{col.ColumnName}", "必填字段"));
                    continue;
                }
                if (rawValue is null) continue;

                // 类型验证（复用基础类型验证器）
                if (col.DataType == EavDataTypes.Composite)
                {
                    if (rawValue is not DynamicCompositeValue cv)
                    {
                        errors.Add(new($"{rowPath}.{col.ColumnName}", "期望组合值"));
                        continue;
                    }
                    var result = _compositeValidator.Validate(cv, col.RefCompositeTypeId!);
                    foreach (var e in result.Errors)
                        errors.Add(new($"{rowPath}.{col.ColumnName}.{e.Field}", e.Message));
                }
                else
                {
                    // 构造临时 AttributeDefinition 复用基础验证器
                    var tempDef = new AttributeDefinition
                    {
                        AttributeName = col.ColumnName,
                        DataType = col.DataType,
                        IsRequired = col.IsRequired,
                        ValidationRule = col.ValidationRule,
                        AllowedValues = col.AllowedValues
                    };
                    var result = _baseValidator.ValidateBaseValue(tempDef, rawValue);
                    errors.AddRange(result.Errors.Select(e =>
                        new ValidationError($"{rowPath}.{e.Field}", e.Message)));
                }

                // 列内唯一性
                if (col.IsUnique)
                {
                    var key = rawValue.ToString() ?? "";
                    if (!uniqueColumnValues.TryGetValue(col.ColumnName, out var set))
                    {
                        set = new HashSet<string>();
                        uniqueColumnValues[col.ColumnName] = set;
                    }
                    if (!set.Add(key))
                    {
                        errors.Add(new($"{rowPath}.{col.ColumnName}",
                            $"值 '{key}' 在列中重复"));
                    }
                }
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}
```

## 文件 31/41 TreeGraph.Api/Services/CustomTableWriteService.cs

```csharp
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Services;

/// <summary>自定义表写入服务：整表替换（默认）+ 行级增量更新</summary>
public class CustomTableWriteService
{
    private readonly EavDbContext _db;
    private readonly ICustomTableCache _tableCache;
    private readonly CustomTableValidationService _validator;
    private readonly CompositeValueService _composite;

    public CustomTableWriteService(
        EavDbContext db,
        ICustomTableCache tableCache,
        CustomTableValidationService validator,
        CompositeValueService composite)
    {
        _db = db;
        _tableCache = tableCache;
        _validator = validator;
        _composite = composite;
    }

    /// <summary>整表替换：删除旧行，写入新行</summary>
    public async Task ReplaceAsync(
        string parentEntityId, string parentEntityType,
        string attributeId, string tableDefinitionId,
        CustomTableValue value, CancellationToken ct = default)
    {
        var table = _tableCache.GetTable(tableDefinitionId);

        foreach (var row in value.Rows)
            NormalizeRow(row, table);

        var result = _validator.Validate(value, tableDefinitionId);
        if (!result.IsValid)
            throw new EavValidationException(result.Errors);

        var oldRows = await _db.CustomTableRows
            .Where(r => r.ParentEntityType == parentEntityType
                     && r.ParentEntityId == parentEntityId
                     && r.AttributeId == attributeId)
            .ToListAsync(ct);
        _db.CustomTableRows.RemoveRange(oldRows);

        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < value.Rows.Count; i++)
        {
            var row = value.Rows[i];
            _db.CustomTableRows.Add(new CustomTableRow
            {
                TableDefinitionId = tableDefinitionId,
                AttributeId = attributeId,
                ParentEntityId = parentEntityId,
                ParentEntityType = parentEntityType,
                RowData = SerializeRow(row, table),
                RowOrder = i,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 新增或更新单行（RowId 为 null 时新增；更新时校验归属）。
    ///
    /// ★ 修复：写入前必须走验证管道（必填/类型/行内唯一），
    ///   并额外做一次跨行唯一性检查（DB 已存在的其它行）。
    /// </summary>
    public async Task UpsertRowAsync(
        string parentEntityId, string parentEntityType,
        string attributeId, string tableDefinitionId,
        CustomTableRowValue rowValue, CancellationToken ct = default)
    {
        var table = _tableCache.GetTable(tableDefinitionId);
        NormalizeRow(rowValue, table);

        // ① 复用整表验证器：检查必填 / 类型 / 行内唯一
        var validationValue = new CustomTableValue
        {
            TableName = table.TableName,
            Rows = new List<CustomTableRowValue> { rowValue }
        };
        var validationResult = _validator.Validate(validationValue, tableDefinitionId);
        if (!validationResult.IsValid)
            throw new EavValidationException(validationResult.Errors);

        // ② 跨行唯一性：与 DB 中同一父实体、同一属性下的其它行比对
        await CheckCrossRowUniquenessAsync(
            parentEntityId, parentEntityType, attributeId,
            table, rowValue, ct);

        if (rowValue.RowId is null)
        {
            _db.CustomTableRows.Add(new CustomTableRow
            {
                TableDefinitionId = tableDefinitionId,
                AttributeId = attributeId,
                ParentEntityId = parentEntityId,
                ParentEntityType = parentEntityType,
                RowData = SerializeRow(rowValue, table),
                RowOrder = rowValue.RowOrder,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            // ★ 修复 P0-3：校验归属，防止越权修改其它实体的行
            var existing = await _db.CustomTableRows
                .FirstOrDefaultAsync(r =>
                    r.RowId == rowValue.RowId
                 && r.ParentEntityId == parentEntityId
                 && r.ParentEntityType == parentEntityType
                 && r.AttributeId == attributeId
                 && r.TableDefinitionId == tableDefinitionId, ct)
                ?? throw new KeyNotFoundException(
                    $"行不存在或不属于当前实体: {rowValue.RowId}");

            existing.RowData = SerializeRow(rowValue, table);
            existing.RowOrder = rowValue.RowOrder;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>删除单行（★ 修复 P0-3：带归属校验）</summary>
    public async Task DeleteRowAsync(
        string rowId, string parentEntityId, string parentEntityType,
        string attributeId, CancellationToken ct = default)
    {
        var row = await _db.CustomTableRows
            .FirstOrDefaultAsync(r =>
                r.RowId == rowId
             && r.ParentEntityId == parentEntityId
             && r.ParentEntityType == parentEntityType
             && r.AttributeId == attributeId, ct);
        if (row is null) return;
        _db.CustomTableRows.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    // ---------- 跨行唯一性 ----------

    /// <summary>
    /// 对表定义中所有 IsUnique = true 的列，检查当前行值是否与
    /// 同一父实体、同一属性下的其它行冲突。
    ///
    /// 实现要点：先把当前行按落库格式序列化为 JSON，再与 DB 中
    /// 其它行的 RowData 逐字段做 JSON 字面量比较，保证与存储格式一致。
    /// </summary>
    private async Task CheckCrossRowUniquenessAsync(
        string parentEntityId, string parentEntityType, string attributeId,
        CustomTableDefinition table, CustomTableRowValue rowValue,
        CancellationToken ct)
    {
        var uniqueCols = table.Columns.Where(c => c.IsUnique).ToList();
        if (uniqueCols.Count == 0) return;

        // 查询同一父实体、同一属性下、除当前行以外的其它行数据
        IQueryable<CustomTableRow> query = _db.CustomTableRows
            .Where(r => r.ParentEntityType == parentEntityType
                     && r.ParentEntityId == parentEntityId
                     && r.AttributeId == attributeId);

        if (rowValue.RowId is string excludeId)
            query = query.Where(r => r.RowId != excludeId);

        var otherRows = await query
            .Select(r => r.RowData)
            .AsNoTracking()
            .ToListAsync(ct);

        if (otherRows.Count == 0) return;

        // 用与落库一致的格式序列化当前行，保证字面量比较可靠
        using var currentDoc = SerializeRow(rowValue, table);
        var currentRoot = currentDoc.RootElement;

        var errors = new List<ValidationError>();
        foreach (var col in uniqueCols)
        {
            if (!currentRoot.TryGetProperty(col.ColumnName, out var curElem))
                continue;
            if (curElem.ValueKind == JsonValueKind.Null) continue;

            var key = curElem.GetRawText();  // JSON 字面量表示（含引号）

            foreach (var otherDoc in otherRows)
            {
                if (otherDoc.RootElement.TryGetProperty(col.ColumnName, out var otherElem)
                    && otherElem.ValueKind != JsonValueKind.Null
                    && otherElem.GetRawText() == key)
                {
                    errors.Add(new ValidationError(
                        col.ColumnName,
                        $"值 {key} 在列中已存在（跨行唯一）"));
                    break;
                }
            }
        }

        if (errors.Count > 0)
            throw new EavValidationException(errors);
    }

    // ---------- 请求值规范化 ----------

    private void NormalizeRow(CustomTableRowValue row, CustomTableDefinition table)
    {
        foreach (var col in table.Columns)
        {
            if (!row.Fields.TryGetValue(col.ColumnName, out var raw) || raw is null)
                continue;
            row.Fields[col.ColumnName] = NormalizeFieldValue(raw, col);
        }
    }

    private object? NormalizeFieldValue(object raw, CustomTableColumn col)
    {
        if (raw is not JsonElement elem) return raw;
        if (elem.ValueKind == JsonValueKind.Null) return null;

        if (col.DataType == EavDataTypes.Composite)
        {
            using var doc = JsonDocument.Parse(elem.GetRawText());
            return _composite.Deserialize(doc, col.RefCompositeTypeId!);
        }

        return col.DataType switch
        {
            EavDataTypes.Int => elem.ValueKind == JsonValueKind.Number
                ? (object)elem.GetInt64()
                : long.TryParse(elem.GetString(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var l) ? l : (object)elem.GetRawText(),
            EavDataTypes.Decimal => elem.ValueKind == JsonValueKind.Number
                ? (object)elem.GetDecimal()
                : decimal.TryParse(elem.GetString(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var d) ? d : (object)elem.GetRawText(),
            EavDataTypes.Bool => elem.GetBoolean(),
            EavDataTypes.Datetime => elem.GetDateTimeOffset(),
            EavDataTypes.Date => DateOnly.Parse(elem.GetString()!),
            EavDataTypes.Time => TimeOnly.Parse(elem.GetString()!),
            EavDataTypes.Json or EavDataTypes.File => JsonDocument.Parse(elem.GetRawText()),
            _ => elem.ValueKind == JsonValueKind.String ? elem.GetString() : elem.GetRawText()
        };
    }

    // ---------- 行序列化 ----------

    private JsonDocument SerializeRow(CustomTableRowValue row, CustomTableDefinition table)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var col in table.Columns)
        {
            row.Fields.TryGetValue(col.ColumnName, out var raw);
            dict[col.ColumnName] = SerializeFieldValue(raw, col);
        }
        return JsonDocument.Parse(JsonSerializer.Serialize(dict));
    }

    private object? SerializeFieldValue(object? value, CustomTableColumn col)
    {
        if (value is null) return null;

        if (col.DataType == EavDataTypes.Composite && value is DynamicCompositeValue cv)
        {
            var doc = _composite.Serialize(cv, col.RefCompositeTypeId!);
            return doc.RootElement.Clone();
        }

        return col.DataType switch
        {
            EavDataTypes.Datetime when value is DateTimeOffset dto => dto,
            EavDataTypes.Date when value is DateOnly d => d.ToString("yyyy-MM-dd"),
            EavDataTypes.Time when value is TimeOnly t => t.ToString("HH:mm:ss"),
            _ => value
        };
    }
}
```

## 文件 32/41 TreeGraph.Api/Services/DynamicCompositeValue.cs

```csharp
namespace TreeGraph.Api.Services;

/// <summary>组合类型运行时值模型（字段名 -> 值）</summary>
public class DynamicCompositeValue
{
    private readonly Dictionary<string, object?> _fields = new();
    public string TypeName { get; }

    public DynamicCompositeValue(string typeName) => TypeName = typeName;

    public object? this[string fieldName]
    {
        get => _fields.TryGetValue(fieldName, out var v) ? v : null;
        set => _fields[fieldName] = value;
    }

    public IReadOnlyDictionary<string, object?> Fields => _fields;

    public bool TryGet<T>(string fieldName, out T? value)
    {
        if (_fields.TryGetValue(fieldName, out var raw) && raw is T t)
        {
            value = t;
            return true;
        }
        value = default;
        return false;
    }
}
```

## 文件 33/41 TreeGraph.Api/Services/EavJsonConverters.cs

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TreeGraph.Api.Services;

/// <summary>数量运行时值序列化为 { value, unitId }（输入走 EAV 管道，不支持直接反序列化）</summary>
public sealed class NumericValueJsonConverter : JsonConverter<NumericValue>
{
    public override NumericValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("数量值请通过 EAV 属性写入管道提交（裸数值或 { value, unitId }）");

    public override void Write(Utf8JsonWriter writer, NumericValue value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("value", value.Value);
        if (value.UnitId is { } uid)
            writer.WriteString("unitId", uid);
        else
            writer.WriteNull("unitId");
        writer.WriteEndObject();
    }
}

/// <summary>组合类型运行时值序列化为字段字典（输入走 EAV 管道，不支持直接反序列化）</summary>
public sealed class DynamicCompositeValueJsonConverter : JsonConverter<DynamicCompositeValue>
{
    public override DynamicCompositeValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("组合类型值请通过 EAV 属性写入管道提交");

    public override void Write(Utf8JsonWriter writer, DynamicCompositeValue value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (name, val) in value.Fields)
        {
            writer.WritePropertyName(name);
            if (val is null)
                writer.WriteNullValue();
            else
                JsonSerializer.Serialize(writer, val, val.GetType(), options);
        }
        writer.WriteEndObject();
    }
}
```

## 文件 34/41 TreeGraph.Api/Services/EavQueryService.cs

```csharp
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Services;

/// <summary>动态查询服务（纯 LINQ）：多个过滤器用 Intersect 实现 AND 语义</summary>
public class EavQueryService
{
    private readonly EavDbContext _db;
    private readonly IAttributeCache _attrCache;
    private readonly ICompositeTypeCache _compositeCache;
    private readonly EavReadService _readService;
    private readonly CompositeValueService _composite;
    private readonly IUnitCache _unitCache;
    private readonly UnitConverter _converter;

    public EavQueryService(
        EavDbContext db, IAttributeCache attrCache, ICompositeTypeCache compositeCache,
        EavReadService readService, CompositeValueService composite,
        IUnitCache unitCache, UnitConverter converter)
    {
        _db = db;
        _attrCache = attrCache;
        _compositeCache = compositeCache;
        _readService = readService;
        _composite = composite;
        _unitCache = unitCache;
        _converter = converter;
    }

    public async Task<PagedResult<string>> FilterEntityIdsAsync(
        EavQueryRequest request, CancellationToken ct = default)
    {
        var definitions = _attrCache.GetDefinitions(request.EntityType)
            .ToDictionary(d => d.AttributeName);

        IQueryable<string>? entityQuery = null;

        foreach (var filter in request.Filters)
        {
            if (!definitions.TryGetValue(filter.AttributeName, out var def))
                throw new ArgumentException($"未知属性: {filter.AttributeName}");
            if (!def.IsSearchable)
                throw new ArgumentException($"属性不可搜索: {filter.AttributeName}");

            var subQuery = BuildSingleFilterQuery(request.EntityType, def, filter);
            entityQuery = entityQuery is null ? subQuery : entityQuery.Intersect(subQuery);
        }

        entityQuery ??= _db.AttributeValues
            .Where(v => v.EntityType == request.EntityType)
            .Select(v => v.EntityId)
            .Distinct();

        var total = await entityQuery.CountAsync(ct);
        var skip = (request.Page - 1) * request.PageSize;
        var take = request.PageSize;

        List<string> ids;

        if (string.IsNullOrEmpty(request.OrderByAttribute))
        {
            // 无排序：SQL 端分页
            ids = await entityQuery
                .OrderBy(id => id)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
        }
        else
        {
            // 有排序：先取全部过滤结果 + 排序键，内存排序后再分页。
            //
            // 原因：Distinct + ORDER BY(相关子查询) 会被 EF Core 静默丢弃 ORDER BY
            // （PostgreSQL 要求 SELECT DISTINCT 的 ORDER BY 表达式出现在 select list 中）。
            // 详见回归测试 Query_OrderByDecimalDesc_SortsCorrectly 的修复背景。
            if (!definitions.TryGetValue(request.OrderByAttribute, out var orderDef))
                throw new ArgumentException($"未知排序属性: {request.OrderByAttribute}");
            if (!orderDef.IsSortable)
                throw new ArgumentException($"属性不可排序: {request.OrderByAttribute}");

            var allIds = await entityQuery.ToListAsync(ct);
            var keyMap = await LoadSortKeysAsync(
                request.EntityType, orderDef, allIds, ct);

            ids = (request.OrderDescending
                ? allIds.OrderByDescending(id => GetKey(keyMap, id)).ThenBy(id => id)
                : allIds.OrderBy(id => GetKey(keyMap, id)).ThenBy(id => id))
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        return new PagedResult<string>
        {
            Items = ids,
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    /// <summary>排序键缺失时返回 null（DESC 排最后，ASC 排最前——与 C# LINQ 默认一致）。</summary>
    private static object? GetKey(IReadOnlyDictionary<string, object?> map, string id)
        => map.TryGetValue(id, out var v) ? v : null;

    /// <summary>
    /// 加载所有过滤结果的排序键。
    /// 返回的 Dictionary 值与 AttributeValue 对应列 C# 类型一致
    /// （long / decimal / string / bool / DateTimeOffset / DateOnly / TimeOnly）。
    /// 每个实体最多一行（uq_av_entity_attr 保证）。
    /// </summary>
    private async Task<Dictionary<string, object?>> LoadSortKeysAsync(
        string entityType,
        AttributeDefinition def,
        IReadOnlyList<string> entityIds,
        CancellationToken ct)
    {
        if (entityIds.Count == 0) return new Dictionary<string, object?>();

        var baseQuery = _db.AttributeValues
            .Where(v => v.EntityType == entityType
                     && v.AttributeId == def.AttributeId
                     && entityIds.Contains(v.EntityId));

        return def.DataType switch
        {
            EavDataTypes.Int => (await baseQuery
                .Select(v => new { v.EntityId, v.ValueInt })
                .ToListAsync(ct))
                .ToDictionary(x => x.EntityId, x => (object?)x.ValueInt),

            EavDataTypes.Decimal => (await baseQuery
                .Select(v => new { v.EntityId, v.ValueDecimal })
                .ToListAsync(ct))
                .ToDictionary(x => x.EntityId, x => (object?)x.ValueDecimal),

            EavDataTypes.String or EavDataTypes.SingleChoice => (await baseQuery
                .Select(v => new { v.EntityId, v.ValueString })
                .ToListAsync(ct))
                .ToDictionary(x => x.EntityId, x => (object?)x.ValueString),

            EavDataTypes.Bool => (await baseQuery
                .Select(v => new { v.EntityId, v.ValueBool })
                .ToListAsync(ct))
                .ToDictionary(x => x.EntityId, x => (object?)x.ValueBool),

            EavDataTypes.Datetime => (await baseQuery
                .Select(v => new { v.EntityId, v.ValueDatetime })
                .ToListAsync(ct))
                .ToDictionary(x => x.EntityId, x => (object?)x.ValueDatetime),

            EavDataTypes.Date => (await baseQuery
                .Select(v => new { v.EntityId, v.ValueDateOnly })
                .ToListAsync(ct))
                .ToDictionary(x => x.EntityId, x => (object?)x.ValueDateOnly),

            EavDataTypes.Time => (await baseQuery
                .Select(v => new { v.EntityId, v.ValueTime })
                .ToListAsync(ct))
                .ToDictionary(x => x.EntityId, x => (object?)x.ValueTime),

            _ => throw new NotSupportedException(
                $"不支持按 {def.DataType} 类型排序")
        };
    }

    public async Task<PagedResult<DynamicEntity>> QueryAsync(
        EavQueryRequest request, CancellationToken ct = default)
    {
        var idResult = await FilterEntityIdsAsync(request, ct);
        var entities = await _readService.LoadBatchAsync(
            idResult.Items, request.EntityType, originalUnits: false, ct);

        return new PagedResult<DynamicEntity>
        {
            Items = entities,
            Total = idResult.Total,
            Page = idResult.Page,
            PageSize = idResult.PageSize
        };
    }

    private IQueryable<string> BuildSingleFilterQuery(
        string entityType, AttributeDefinition def, AttributeFilter filter)
    {
        if (def.DataType == EavDataTypes.Composite)
            return BuildCompositeFilterQuery(entityType, def, filter);

        var baseQuery = _db.AttributeValues
            .Where(v => v.EntityType == entityType && v.AttributeId == def.AttributeId);

        if (filter.Operator == "unit_eq")
        {
            var unitId = Guid.Parse(filter.Value!.ToString()!);
            return baseQuery
                .Where(x => x.UnitId == unitId)
                .Select(x => x.EntityId)
                .Distinct();
        }

        var filtered = def.DataType switch
        {
            EavDataTypes.Int => ApplyIntFilter(baseQuery, def, filter),
            EavDataTypes.Decimal => ApplyDecimalFilter(baseQuery, def, filter),
            EavDataTypes.String => ApplyStringFilter(baseQuery, filter),
            EavDataTypes.SingleChoice => ApplySingleChoiceFilter(baseQuery, filter),
            EavDataTypes.Bool => ApplyBoolFilter(baseQuery, filter),
            EavDataTypes.Datetime => ApplyDatetimeFilter(baseQuery, filter),
            EavDataTypes.Date => ApplyDateFilter(baseQuery, filter),
            EavDataTypes.Time => ApplyTimeFilter(baseQuery, filter),
            _ => throw new NotSupportedException($"不支持的类型: {def.DataType}")
        };

        return filtered.Select(v => v.EntityId).Distinct();
    }

    private IQueryable<AttributeValue> ApplyIntFilter(
        IQueryable<AttributeValue> q, AttributeDefinition def, AttributeFilter f)
    {
        // ★ 先处理 in：f.Value 是数组，不能走 ParseNumericFilterValue
        if (f.Operator == "in")
        {
            var values = InLongs(f.Value);
            if (values.Length == 0)
                return q.Where(_ => false);   // 空集合 → 无结果
            return q.Where(x => x.ValueInt != null
                             && values.Contains(x.ValueInt.Value));
        }

        var (rawValue, filterUnitId) = ParseNumericFilterValue(f.Value);
        if (rawValue is null) throw new ArgumentException("过滤值格式错误");
        var v = NormalizeFilterValue(def, rawValue.Value, filterUnitId);
        decimal? v2 = f.Value2 is null ? null : NormalizeFilterValue2(def, f.Value2);

        return f.Operator switch
        {
            "eq" => q.Where(x => x.ValueInt == (long)v),
            "neq" => q.Where(x => x.ValueInt != (long)v),
            "gt" => q.Where(x => x.ValueInt > (long)v),
            "gte" => q.Where(x => x.ValueInt >= (long)v),
            "lt" => q.Where(x => x.ValueInt < (long)v),
            "lte" => q.Where(x => x.ValueInt <= (long)v),
            "between" => q.Where(x => x.ValueInt >= (long)v && x.ValueInt <= (long)v2!.Value),
            _ => throw new NotSupportedException($"不支持的运算符: {f.Operator}")
        };
    }

    private IQueryable<AttributeValue> ApplyDecimalFilter(
        IQueryable<AttributeValue> q, AttributeDefinition def, AttributeFilter f)
    {
        // ★ 先处理 in
        if (f.Operator == "in")
        {
            var values = InDecimals(f.Value);
            if (values.Length == 0)
                return q.Where(_ => false);
            return q.Where(x => x.ValueDecimal != null
                             && values.Contains(x.ValueDecimal.Value));
        }

        var (rawValue, filterUnitId) = ParseNumericFilterValue(f.Value);
        if (rawValue is null) throw new ArgumentException("过滤值格式错误");
        var v = NormalizeFilterValue(def, rawValue.Value, filterUnitId);
        decimal? v2 = f.Value2 is null ? null : NormalizeFilterValue2(def, f.Value2);

        return f.Operator switch
        {
            "eq" => q.Where(x => x.ValueDecimal == v),
            "neq" => q.Where(x => x.ValueDecimal != v),
            "gt" => q.Where(x => x.ValueDecimal > v),
            "gte" => q.Where(x => x.ValueDecimal >= v),
            "lt" => q.Where(x => x.ValueDecimal < v),
            "lte" => q.Where(x => x.ValueDecimal <= v),
            "between" => q.Where(x => x.ValueDecimal >= v && x.ValueDecimal <= v2!.Value),
            _ => throw new NotSupportedException($"不支持的运算符: {f.Operator}")
        };
    }


    private decimal NormalizeFilterValue(
        AttributeDefinition def, decimal value, Guid? filterUnitId)
    {
        if (def.UnitId is not { } baseUnitId) return value;
        if (filterUnitId is null || filterUnitId == baseUnitId) return value;
        return _converter.ToBase(value, filterUnitId.Value, baseUnitId);
    }

    private decimal NormalizeFilterValue2(AttributeDefinition def, object? value2)
    {
        var (rawValue, filterUnitId) = ParseNumericFilterValue(value2);
        if (rawValue is null) throw new ArgumentException("过滤值格式错误");
        return NormalizeFilterValue(def, rawValue.Value, filterUnitId);
    }

    private static (decimal? Value, Guid? UnitId) ParseNumericFilterValue(object? value)
    {
        return value switch
        {
            null => (null, null),
            NumericValue nv => (nv.Value, nv.UnitId),
            JsonElement je when je.ValueKind == JsonValueKind.Object =>
                (je.GetProperty("value").GetDecimal(),
                 je.TryGetProperty("unitId", out var uid) && uid.ValueKind == JsonValueKind.String
                     ? Guid.Parse(uid.GetString()!) : null),
            JsonElement je when je.ValueKind == JsonValueKind.Number => (je.GetDecimal(), null),
            decimal d => (d, null),
            string s when decimal.TryParse(s, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var parsed) => (parsed, null),
            _ => (null, null)
        };
    }

    private static IQueryable<AttributeValue> ApplyStringFilter(
        IQueryable<AttributeValue> q, AttributeFilter f)
    {
        var v = f.Value?.ToString() ?? "";
        return f.Operator switch
        {
            "eq" => q.Where(x => x.ValueString == v),
            "neq" => q.Where(x => x.ValueString != v),
            "like" => q.Where(x => x.ValueString != null && x.ValueString.Contains(v)),
            "startswith" => q.Where(x => x.ValueString != null && x.ValueString.StartsWith(v)),
            "endswith" => q.Where(x => x.ValueString != null && x.ValueString.EndsWith(v)),
            "in" => q.Where(x => x.ValueString != null && InStrings(f.Value).Contains(x.ValueString)),
            _ => throw new NotSupportedException($"不支持的运算符: {f.Operator}")
        };
    }

    private static IQueryable<AttributeValue> ApplySingleChoiceFilter(
        IQueryable<AttributeValue> q, AttributeFilter f)
    {
        var v = f.Value?.ToString() ?? "";
        return f.Operator switch
        {
            "eq" => q.Where(x => x.ValueString == v),
            "neq" => q.Where(x => x.ValueString != v),
            "in" => q.Where(x => x.ValueString != null && InStrings(f.Value).Contains(x.ValueString)),
            "nin" => q.Where(x => x.ValueString == null || !InStrings(f.Value).Contains(x.ValueString)),
            _ => throw new NotSupportedException($"不支持的运算符: {f.Operator}")
        };
    }

    private static IQueryable<AttributeValue> ApplyBoolFilter(
        IQueryable<AttributeValue> q, AttributeFilter f)
    {
        var v = ToBool(f.Value);
        return q.Where(x => x.ValueBool == v);
    }

    private static IQueryable<AttributeValue> ApplyDatetimeFilter(
        IQueryable<AttributeValue> q, AttributeFilter f)
    {
        var v = ParseDateTimeOffset(f.Value);
        return f.Operator switch
        {
            "eq" => q.Where(x => x.ValueDatetime == v),
            "gt" => q.Where(x => x.ValueDatetime > v),
            "gte" => q.Where(x => x.ValueDatetime >= v),
            "lt" => q.Where(x => x.ValueDatetime < v),
            "lte" => q.Where(x => x.ValueDatetime <= v),
            "between" => q.Where(x => x.ValueDatetime >= v
                                   && x.ValueDatetime <= ParseDateTimeOffset(f.Value2)),
            _ => throw new NotSupportedException($"不支持的运算符: {f.Operator}")
        };
    }

    private static IQueryable<AttributeValue> ApplyDateFilter(
        IQueryable<AttributeValue> q, AttributeFilter f)
    {
        var v = ParseDateOnly(f.Value);
        return f.Operator switch
        {
            "eq" => q.Where(x => x.ValueDateOnly == v),
            "gt" => q.Where(x => x.ValueDateOnly > v),
            "gte" => q.Where(x => x.ValueDateOnly >= v),
            "lt" => q.Where(x => x.ValueDateOnly < v),
            "lte" => q.Where(x => x.ValueDateOnly <= v),
            "between" => q.Where(x => x.ValueDateOnly >= v
                                   && x.ValueDateOnly <= ParseDateOnly(f.Value2)),
            _ => throw new NotSupportedException($"不支持的运算符: {f.Operator}")
        };
    }

    private static IQueryable<AttributeValue> ApplyTimeFilter(
        IQueryable<AttributeValue> q, AttributeFilter f)
    {
        var v = TimeOnly.Parse(f.Value!.ToString()!);
        return f.Operator switch
        {
            "eq" => q.Where(x => x.ValueTime == v),
            "gt" => q.Where(x => x.ValueTime > v),
            "gte" => q.Where(x => x.ValueTime >= v),
            "lt" => q.Where(x => x.ValueTime < v),
            "lte" => q.Where(x => x.ValueTime <= v),
            "between" => q.Where(x => x.ValueTime >= v
                                   && x.ValueTime <= TimeOnly.Parse(f.Value2!.ToString()!)),
            _ => throw new NotSupportedException($"不支持的运算符: {f.Operator}")
        };
    }

    // ============================================================
    // 组合类型查询
    // ============================================================

    private IQueryable<string> BuildCompositeFilterQuery(
        string entityType, AttributeDefinition def, AttributeFilter filter)
    {
        if (string.IsNullOrEmpty(filter.FieldPath))
            throw new ArgumentException("组合类型查询必须指定 FieldPath");

        var fieldPath = filter.FieldPath.Split('.');
        var fieldDef = ResolveCompositeField(def, fieldPath)
            ?? throw new ArgumentException($"未知字段: {filter.FieldPath}");

        if (!fieldDef.IsSearchable)
            throw new ArgumentException($"字段不可搜索: {filter.FieldPath}");

        if (fieldPath.Length > 2)
            throw new NotSupportedException("组合类型查询最多支持两层字段路径");

        var query = _db.AttributeValues
            .Where(v => v.EntityType == entityType && v.AttributeId == def.AttributeId);

        var fieldName = fieldPath[0];

        if (fieldPath.Length == 1)
        {
            // ★ 带单位的组合内 decimal：JSONB 内可能存为 { value, unitId } 或裸数值，
            //   需要专用查询路径
            if (fieldDef.DataType == EavDataTypes.Decimal && fieldDef.UnitId is not null)
            {
                query = ApplyCompositeDecimalFilterWithUnit(query, fieldName, filter);
            }
            else
            {
                query = fieldDef.DataType switch
                {
                    EavDataTypes.Int => filter.Operator switch
                    {
                        "eq" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetInt64() == ToInt64(filter.Value)),
                        "gt" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetInt64() > ToInt64(filter.Value)),
                        "gte" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetInt64() >= ToInt64(filter.Value)),
                        "lt" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetInt64() < ToInt64(filter.Value)),
                        "lte" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetInt64() <= ToInt64(filter.Value)),
                        _ => throw new NotSupportedException($"不支持的运算符: {filter.Operator}")
                    },
                    EavDataTypes.Decimal => filter.Operator switch
                    {
                        "eq" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetDecimal() == ToDecimal(filter.Value)),
                        "gt" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetDecimal() > ToDecimal(filter.Value)),
                        "gte" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetDecimal() >= ToDecimal(filter.Value)),
                        "lt" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetDecimal() < ToDecimal(filter.Value)),
                        "lte" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetDecimal() <= ToDecimal(filter.Value)),
                        _ => throw new NotSupportedException($"不支持的运算符: {filter.Operator}")
                    },
                    _ => ApplyJsonbStringFilter(query, fieldName, filter)
                };
            }
        }
        else
        {
            var nestedName = fieldPath[1];

            // ★ 嵌套带单位 decimal 同样处理
            if (fieldDef.DataType == EavDataTypes.Decimal && fieldDef.UnitId is not null)
            {
                query = ApplyCompositeNestedDecimalFilterWithUnit(
                    query, fieldName, nestedName, filter);
            }
            else
            {
                query = fieldDef.DataType switch
                {
                    EavDataTypes.Int => filter.Operator switch
                    {
                        "eq" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetProperty(nestedName)
                            .GetInt64() == ToInt64(filter.Value)),
                        "gt" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetProperty(nestedName)
                            .GetInt64() > ToInt64(filter.Value)),
                        "gte" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetProperty(nestedName)
                            .GetInt64() >= ToInt64(filter.Value)),
                        "lt" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetProperty(nestedName)
                            .GetInt64() < ToInt64(filter.Value)),
                        "lte" => query.Where(v => v.ValueJsonb!.RootElement
                            .GetProperty(fieldName).GetProperty(nestedName)
                            .GetInt64() <= ToInt64(filter.Value)),
                        _ => throw new NotSupportedException($"不支持的运算符: {filter.Operator}")
                    },
                    _ => ApplyJsonbStringFilterNested(query, fieldName, nestedName, filter)
                };
            }
        }

        return query.Select(v => v.EntityId).Distinct();
    }

    /// <summary>
    /// 组合内带单位 decimal 字段的单层查询。
    ///
    /// 存储形态（见 CompositeValueService.Serialize）：
    ///   1) 裸数值 { "field": 1.5 }                          — 用户输入未指定单位
    ///   2) 对象   { "field": { "value": 1.5, "unitId": ".." }} — 用户输入指定了单位
    ///
    /// ★ 用 jsonb_typeof + CASE（三元表达式）抽取数值：
    ///   - 两种形态统一比较；其它类型（如 string）落到 NULL 被比较排除。
    ///   - 不能用 JsonElement.ValueKind 做判断——EF 无法翻译为 SQL。
    ///   - CASE 只计算命中的分支，number 行不会触发 object 取值（反之亦然），安全。
    /// </summary>
    private static IQueryable<AttributeValue> ApplyCompositeDecimalFilterWithUnit(
        IQueryable<AttributeValue> query, string fieldName, AttributeFilter filter)
    {
        var target = ToDecimal(filter.Value);

        return filter.Operator switch
        {
            "eq" => query.Where(v =>
                (EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(fieldName)) == "number"
                    ? v.ValueJsonb!.RootElement.GetProperty(fieldName).GetDecimal()
                    : EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(fieldName)) == "object"
                        ? v.ValueJsonb!.RootElement.GetProperty(fieldName).GetProperty("value").GetDecimal()
                        : (decimal?)null) == target),

            "gt" => query.Where(v =>
                (EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(fieldName)) == "number"
                    ? v.ValueJsonb!.RootElement.GetProperty(fieldName).GetDecimal()
                    : EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(fieldName)) == "object"
                        ? v.ValueJsonb!.RootElement.GetProperty(fieldName).GetProperty("value").GetDecimal()
                        : (decimal?)null) > target),

            "gte" => query.Where(v =>
                (EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(fieldName)) == "number"
                    ? v.ValueJsonb!.RootElement.GetProperty(fieldName).GetDecimal()
                    : EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(fieldName)) == "object"
                        ? v.ValueJsonb!.RootElement.GetProperty(fieldName).GetProperty("value").GetDecimal()
                        : (decimal?)null) >= target),

            "lt" => query.Where(v =>
                (EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(fieldName)) == "number"
                    ? v.ValueJsonb!.RootElement.GetProperty(fieldName).GetDecimal()
                    : EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(fieldName)) == "object"
                        ? v.ValueJsonb!.RootElement.GetProperty(fieldName).GetProperty("value").GetDecimal()
                        : (decimal?)null) < target),

            "lte" => query.Where(v =>
                (EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(fieldName)) == "number"
                    ? v.ValueJsonb!.RootElement.GetProperty(fieldName).GetDecimal()
                    : EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(fieldName)) == "object"
                        ? v.ValueJsonb!.RootElement.GetProperty(fieldName).GetProperty("value").GetDecimal()
                        : (decimal?)null) <= target),

            _ => throw new NotSupportedException($"不支持的运算符: {filter.Operator}")
        };
    }

    /// <summary>
    /// 嵌套（两层）带单位 decimal 字段查询。抽取策略同单层：
    /// jsonb_typeof + CASE，EF.Functions.JsonTypeof 可翻译为 jsonb_typeof。
    /// </summary>
    private static IQueryable<AttributeValue> ApplyCompositeNestedDecimalFilterWithUnit(
        IQueryable<AttributeValue> query, string outerName, string innerName, AttributeFilter filter)
    {
        var target = ToDecimal(filter.Value);

        return filter.Operator switch
        {
            "eq" => query.Where(v =>
                (EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName)) == "number"
                    ? v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName).GetDecimal()
                    : EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName)) == "object"
                        ? v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName).GetProperty("value").GetDecimal()
                        : (decimal?)null) == target),

            "gt" => query.Where(v =>
                (EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName)) == "number"
                    ? v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName).GetDecimal()
                    : EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName)) == "object"
                        ? v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName).GetProperty("value").GetDecimal()
                        : (decimal?)null) > target),

            "gte" => query.Where(v =>
                (EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName)) == "number"
                    ? v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName).GetDecimal()
                    : EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName)) == "object"
                        ? v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName).GetProperty("value").GetDecimal()
                        : (decimal?)null) >= target),

            "lt" => query.Where(v =>
                (EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName)) == "number"
                    ? v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName).GetDecimal()
                    : EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName)) == "object"
                        ? v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName).GetProperty("value").GetDecimal()
                        : (decimal?)null) < target),

            "lte" => query.Where(v =>
                (EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName)) == "number"
                    ? v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName).GetDecimal()
                    : EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName)) == "object"
                        ? v.ValueJsonb!.RootElement.GetProperty(outerName).GetProperty(innerName).GetProperty("value").GetDecimal()
                        : (decimal?)null) <= target),

            _ => throw new NotSupportedException($"不支持的运算符: {filter.Operator}")
        };
    }

    private static IQueryable<AttributeValue> ApplyJsonbStringFilter(
        IQueryable<AttributeValue> query, string fieldName, AttributeFilter filter)
    {
        var strValue = filter.Value?.ToString() ?? "";
        return filter.Operator switch
        {
            "eq" => query.Where(v => v.ValueJsonb!.RootElement
                .GetProperty(fieldName).GetString() == strValue),
            "like" => query.Where(v => v.ValueJsonb!.RootElement
                .GetProperty(fieldName).GetString()!.Contains(strValue)),
            "startswith" => query.Where(v => v.ValueJsonb!.RootElement
                .GetProperty(fieldName).GetString()!.StartsWith(strValue)),
            "endswith" => query.Where(v => v.ValueJsonb!.RootElement
                .GetProperty(fieldName).GetString()!.EndsWith(strValue)),
            _ => throw new NotSupportedException($"不支持的运算符: {filter.Operator}")
        };
    }

    private static IQueryable<AttributeValue> ApplyJsonbStringFilterNested(
        IQueryable<AttributeValue> query, string fieldName, string nestedName, AttributeFilter filter)
    {
        var strValue = filter.Value?.ToString() ?? "";
        return filter.Operator switch
        {
            "eq" => query.Where(v => v.ValueJsonb!.RootElement
                .GetProperty(fieldName).GetProperty(nestedName).GetString() == strValue),
            "like" => query.Where(v => v.ValueJsonb!.RootElement
                .GetProperty(fieldName).GetProperty(nestedName).GetString()!.Contains(strValue)),
            "startswith" => query.Where(v => v.ValueJsonb!.RootElement
                .GetProperty(fieldName).GetProperty(nestedName).GetString()!.StartsWith(strValue)),
            "endswith" => query.Where(v => v.ValueJsonb!.RootElement
                .GetProperty(fieldName).GetProperty(nestedName).GetString()!.EndsWith(strValue)),
            _ => throw new NotSupportedException($"不支持的运算符: {filter.Operator}")
        };
    }

    private CompositeFieldDefinition? ResolveCompositeField(
        AttributeDefinition def, string[] path)
    {
        if (def.RefCompositeTypeId is null) return null;

        CompositeFieldDefinition? current = null;
        string typeId = def.RefCompositeTypeId;

        foreach (var segment in path)
        {
            var typeDef = _compositeCache.GetType(typeId);
            current = typeDef.Fields
                .FirstOrDefault(f => f.FieldName == segment && !f.IsDeleted);
            if (current is null) return null;

            if (current.DataType == EavDataTypes.Composite
                && current.RefCompositeTypeId is not null)
                typeId = current.RefCompositeTypeId;
        }

        return current;
    }

    // ---------- 工具方法 ----------

    private static long[] InLongs(object? value)
    {
        if (value is JsonElement e && e.ValueKind == JsonValueKind.Array)
        {
            return e.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Number)
                .Select(x => x.GetInt64())
                .ToArray();
        }
        if (value is IEnumerable<long> list) return list.ToArray();
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Number)
            return new[] { je.GetInt64() };
        if (value is long l) return new[] { l };
        if (value is int i) return new[] { (long)i };
        // 非数值 → 返回空数组（等价于"匹配不到"），而不是抛异常
        return Array.Empty<long>();
    }

    private static decimal[] InDecimals(object? value)
    {
        if (value is JsonElement e && e.ValueKind == JsonValueKind.Array)
        {
            return e.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Number)
                .Select(x => x.GetDecimal())
                .ToArray();
        }
        if (value is IEnumerable<decimal> list) return list.ToArray();
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Number)
            return new[] { je.GetDecimal() };
        if (value is decimal d) return new[] { d };
        return Array.Empty<decimal>();
    }

    private static string[] InStrings(object? value)
    {
        if (value is JsonElement e && e.ValueKind == JsonValueKind.Array)
            return e.EnumerateArray().Select(x => x.ToString()).ToArray();
        if (value is IEnumerable<string> list) return list.ToArray();
        return [value?.ToString() ?? ""];
    }

    private static DateTimeOffset ParseDateTimeOffset(object? value)
        => value is DateTimeOffset dto
            ? dto
            : DateTimeOffset.Parse(value!.ToString()!,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private static DateOnly ParseDateOnly(object? value)
        => value is DateOnly d
            ? d
            : DateOnly.Parse(value!.ToString()!, CultureInfo.InvariantCulture);

    private static long ToInt64(object? value) => value switch
    {
        null => throw new ArgumentException("值为 null"),
        JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt64(),
        JsonElement je when je.ValueKind == JsonValueKind.String
            => long.Parse(je.GetString()!, CultureInfo.InvariantCulture),
        long l => l,
        int i => i,
        decimal d => (long)d,
        string s => long.Parse(s, CultureInfo.InvariantCulture),
        _ => Convert.ToInt64(value, CultureInfo.InvariantCulture)
    };

    private static decimal ToDecimal(object? value) => value switch
    {
        null => throw new ArgumentException("值为 null"),
        JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetDecimal(),
        JsonElement je when je.ValueKind == JsonValueKind.String
            => decimal.Parse(je.GetString()!, CultureInfo.InvariantCulture),
        decimal d => d,
        long l => l,
        int i => i,
        double db => (decimal)db,
        string s => decimal.Parse(s, CultureInfo.InvariantCulture),
        _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
    };

    private static bool ToBool(object? value) => value switch
    {
        null => throw new ArgumentException("值为 null"),
        JsonElement je when je.ValueKind == JsonValueKind.True => true,
        JsonElement je when je.ValueKind == JsonValueKind.False => false,
        JsonElement je when je.ValueKind == JsonValueKind.String
            => bool.Parse(je.GetString()!),
        bool b => b,
        _ => Convert.ToBoolean(value, CultureInfo.InvariantCulture)
    };
}
```

## 文件 35/41 TreeGraph.Api/Services/EavReadService.cs

```csharp
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;

namespace TreeGraph.Api.Services;

/// <summary>读取服务：按元数据把类型化值列还原为运行时值</summary>
public class EavReadService
{
    private readonly EavDbContext _db;
    private readonly IAttributeCache _attrCache;
    private readonly CompositeValueService _composite;
    private readonly IUnitCache _unitCache;
    private readonly UnitConverter _converter;
    private readonly IOptionSetCache _optionSetCache;

    public EavReadService(
        EavDbContext db, IAttributeCache attrCache, CompositeValueService composite,
        IUnitCache unitCache, UnitConverter converter, IOptionSetCache optionSetCache)
    {
        _db = db;
        _attrCache = attrCache;
        _composite = composite;
        _unitCache = unitCache;
        _converter = converter;
        _optionSetCache = optionSetCache;
    }

    /// <param name="originalUnits">true 时数量值按原始输入单位还原（UI 展示）；默认返回基准单位</param>
    public async Task<DynamicEntity> LoadAsync(
        string entityId, string entityType, bool originalUnits = false,
        CancellationToken ct = default)
    {
        var definitions = _attrCache.GetDefinitions(entityType);
        var defMap = definitions.ToDictionary(d => d.AttributeId);

        var rows = await _db.AttributeValues
            .Where(v => v.EntityType == entityType && v.EntityId == entityId)
            .AsNoTracking()
            .ToListAsync(ct);

        var result = new DynamicEntity(entityId, entityType);

        // ★ #3：填充 UpdatedAt（供乐观锁使用）
        if (rows.Count > 0)
            result.UpdatedAt = rows.Max(r => r.UpdatedAt);

        foreach (var row in rows)
        {
            if (!defMap.TryGetValue(row.AttributeId, out var def)) continue;
            var value = ExtractTypedValue(row, def, originalUnits);
            result.SetProperty(def.AttributeName, value);
        }

        return result;
    }

    public async Task<List<DynamicEntity>> LoadBatchAsync(
        IEnumerable<string> entityIds, string entityType,
        bool originalUnits = false, CancellationToken ct = default)
    {
        var ids = entityIds.ToList();
        var definitions = _attrCache.GetDefinitions(entityType);
        var defMap = definitions.ToDictionary(d => d.AttributeId);

        var allRows = await _db.AttributeValues
            .Where(v => v.EntityType == entityType && ids.Contains(v.EntityId))
            .AsNoTracking()
            .ToListAsync(ct);

        var grouped = allRows.GroupBy(r => r.EntityId);

        // ★ 保持输入 ids 的顺序——FilterEntityIdsAsync 已按业务键排好序，
        //   GroupBy 的输出顺序由 DB 行序决定，必须显式按输入序重排。
        var inputOrder = ids
            .Select((id, idx) => (id, idx))
            .ToDictionary(x => x.id, x => x.idx);

        return grouped.Select(g =>
        {
            var entity = new DynamicEntity(g.Key, entityType);
            entity.UpdatedAt = g.Max(r => r.UpdatedAt);  // ★
            foreach (var row in g)
            {
                if (defMap.TryGetValue(row.AttributeId, out var def))
                {
                    entity.SetProperty(def.AttributeName,
                        ExtractTypedValue(row, def, originalUnits));
                }
            }
            return entity;
        })
        .OrderBy(e => inputOrder.GetValueOrDefault(e.EntityId, int.MaxValue))
        .ToList();
    }

    private object? ExtractTypedValue(AttributeValue row, AttributeDefinition def, bool originalUnits)
    {
        return def.DataType switch
        {
            EavDataTypes.String => row.ValueString,
            EavDataTypes.SingleChoice => ExtractSingleChoice(row, def),
            EavDataTypes.Int => ExtractNumericValue(
                row.ValueInt is { } i ? i : null, row.UnitId, def, originalUnits),
            EavDataTypes.Decimal => ExtractNumericValue(
                row.ValueDecimal, row.UnitId, def, originalUnits),
            EavDataTypes.Bool => row.ValueBool,
            EavDataTypes.Datetime => row.ValueDatetime,
            EavDataTypes.Date => row.ValueDateOnly,
            EavDataTypes.Time => row.ValueTime,
            EavDataTypes.File => row.ValueFileMeta,
            EavDataTypes.Json => row.ValueJsonb,
            EavDataTypes.Composite => row.ValueJsonb is null
                ? null
                // ★ #4：组合反序列化支持 originalUnits 参数
                : _composite.Deserialize(
                    row.ValueJsonb, def.RefCompositeTypeId!, originalUnits),
            _ => null
        };
    }

    private object? ExtractSingleChoice(AttributeValue row, AttributeDefinition def)
    {
        if (row.ValueString is null) return null;
        if (def.RefOptionSetId is not { } sid) return row.ValueString;

        try
        {
            var item = _optionSetCache.GetSet(sid).Items
                .FirstOrDefault(i => i.Value == row.ValueString);
            return new SingleChoiceValue(row.ValueString, item?.Label ?? row.ValueString);
        }
        catch (KeyNotFoundException)
        {
            return row.ValueString;
        }
    }

    /// <summary>
    /// 数值列还原。
    ///
    /// ★ 修复：当属性未绑定基准单位时，int 类型必须返回 long（而非 decimal），
    /// 否则前端会收到 decimal 的 JSON 数字（含小数位）与 API 契约不符。
    /// </summary>
    private object? ExtractNumericValue(
        decimal? baseValue, Guid? originalUnitId, AttributeDefinition def, bool originalUnits)
    {
        if (baseValue is null) return null;

        if (def.UnitId is not { } baseUnitId)
        {
            // 无单位绑定：按数据类型返回对应 C# 类型
            return def.DataType == EavDataTypes.Int
                ? (object)(long)baseValue.Value
                : baseValue.Value;
        }

        if (!originalUnits)
            return new NumericValue(baseValue.Value, baseUnitId);

        if (originalUnitId is not { } ou || ou == baseUnitId)
            return new NumericValue(baseValue.Value, baseUnitId);

        try
        {
            var restored = _converter.FromBase(baseValue.Value, baseUnitId, ou);
            return new NumericValue(restored, ou);
        }
        catch (InvalidOperationException)
        {
            return new NumericValue(baseValue.Value, baseUnitId);
        }
    }

    public async Task<List<AttributeAuditLog>> GetHistoryAsync(
        string entityId, string entityType, DateTimeOffset? from = null,
        CancellationToken ct = default)
    {
        var query = _db.AttributeAuditLogs
            .Where(a => a.EntityId == entityId && a.EntityType == entityType);

        if (from.HasValue)
            query = query.Where(a => a.ChangedAt >= from.Value);

        return await query
            .OrderByDescending(a => a.ChangedAt)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}

/// <summary>动态实体容器</summary>
public class DynamicEntity
{
    private readonly Dictionary<string, object?> _props = new();

    /// <summary>实体 ID（GUID 字符串）。</summary>
    public string EntityId { get; }
    public string EntityType { get; }

    /// <summary>
    /// ★ #3：实体所有 AttributeValue 行的最大 UpdatedAt。
    /// 无属性值时（新建）为 null。
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    public DynamicEntity(string id, string type) { EntityId = id; EntityType = type; }

    public void SetProperty(string name, object? value) => _props[name] = value;
    public object? GetProperty(string name) => _props.TryGetValue(name, out var v) ? v : null;
    public IReadOnlyDictionary<string, object?> Properties => _props;
}
```

## 文件 36/41 TreeGraph.Api/Services/EavValidationService.cs

```csharp
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Services;

/// <summary>EAV 验证引擎：必填 -> 类型转换 -> 枚举约束 -> 规则校验</summary>
public class EavValidationService
{
    private readonly ICompositeTypeCache _compositeCache;
    private readonly IUnitCache _unitCache;
    private readonly UnitConverter _converter;
    private readonly IOptionSetCache _optionSetCache;

    public EavValidationService(
        ICompositeTypeCache compositeCache,
        IUnitCache unitCache,
        UnitConverter converter,
        IOptionSetCache optionSetCache)
    {
        _compositeCache = compositeCache;
        _unitCache = unitCache;
        _converter = converter;
        _optionSetCache = optionSetCache;
    }

    /// <summary>
    /// 验证属性值（基础类型）。
    ///
    /// ★ 防御性检查：
    ///   - int 类型不允许绑定单位（元数据配置错误）。
    ///   - int 类型不接受小数（否则写入 bigint 会静默截断）。
    /// 即使数据库中被绕过（旧版本写入、直接 SQL），写入时也会在此处拦截。
    /// </summary>
    public ValidationResult ValidateBaseValue(AttributeDefinition def, object? value)
    {
        var errors = new List<ValidationError>();

        // ★ 元数据一致性防御：int + UnitId 是非法配置
        if (def.DataType == EavDataTypes.Int && def.UnitId is not null)
        {
            errors.Add(new(def.AttributeName,
                "int 类型属性绑定了单位（元数据配置错误）。" +
                "归一化到基准单位会产生小数并被截断，请改用 decimal 类型，或解除单位绑定。"));
            return new ValidationResult(false, errors);
        }

        // 单选类型：必填默认值放行逻辑与基础类型不同，走独立分支
        if (def.DataType == EavDataTypes.SingleChoice)
            return ValidateSingleChoice(def, value);

        // 必填校验
        if (def.IsRequired && value is null)
        {
            errors.Add(new(def.AttributeName, "必填字段"));
            return new ValidationResult(false, errors);
        }
        if (value is null) return new ValidationResult(true, errors);

        // 数量类型：解包 NumericValue，校验单位并归一化
        if (def.DataType is EavDataTypes.Int or EavDataTypes.Decimal)
        {
            var (numericValue, unitId) = UnwrapNumeric(value);
            if (numericValue is null)
            {
                errors.Add(new(def.AttributeName, "数值格式错误"));
                return new ValidationResult(false, errors);
            }

            // ★ int 类型必须为整数，否则写入 bigint 会静默截断
            if (def.DataType == EavDataTypes.Int
                && numericValue.Value != Math.Truncate(numericValue.Value))
            {
                errors.Add(new(def.AttributeName,
                    $"int 类型不接受小数，收到 {numericValue.Value}"));
                return new ValidationResult(false, errors);
            }

            // 单位分类校验
            if (def.UnitId is { } baseUnitId)
            {
                var baseUnit = _unitCache.Get(baseUnitId);

                if (unitId is { } inputUnitId)
                {
                    var inputUnit = _unitCache.Get(inputUnitId);
                    if (inputUnit.Category != baseUnit.Category)
                    {
                        errors.Add(new(def.AttributeName,
                            $"单位分类不匹配：期望 {baseUnit.Category}，实际 {inputUnit.Category}"));
                    }
                }
            }
            else if (unitId is not null)
            {
                errors.Add(new(def.AttributeName, "该属性未绑定基准单位，不允许指定单位"));
            }

            // 用归一化后的值做范围校验（min/max 始终按基准单位书写）
            var normalized = NormalizeToBase(def, numericValue.Value, unitId);
            ValidateNumericRange(def, normalized, errors);

            return new ValidationResult(errors.Count == 0, errors);
        }

        // 类型转换
        if (!TryConvert(def.DataType, value, out var converted))
        {
            errors.Add(new(def.AttributeName, $"类型不匹配，期望 {def.DataType}"));
            return new ValidationResult(false, errors);
        }

        // 枚举约束
        if (def.AllowedValues is not null && converted is not null)
        {
            var allowed = def.AllowedValues.RootElement.EnumerateArray()
                .Select(x => x.GetString()).ToList();
            if (!allowed.Contains(converted.ToString()))
                errors.Add(new(def.AttributeName, "值不在允许范围内"));
        }

        // 规则校验（正则、日期范围等）
        if (def.ValidationRule is not null)
            ValidateRule(def, converted, errors);

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>单选类型验证：null 且存在默认选项时放行（写入端自动填充默认值），否则校验选项存在性</summary>
    private ValidationResult ValidateSingleChoice(AttributeDefinition def, object? value)
    {
        var errors = new List<ValidationError>();
        var set = def.RefOptionSetId is { } sid ? _optionSetCache.GetSet(sid) : null;

        if (value is null)
        {
            if (set is { } s && s.Items.Any(i => i.IsDefault))
                return new ValidationResult(true, errors);

            if (def.IsRequired)
                errors.Add(new(def.AttributeName, "必填字段"));

            return new ValidationResult(errors.Count == 0, errors);
        }

        if (set is null)
        {
            errors.Add(new(def.AttributeName, "属性未绑定选项集"));
            return new ValidationResult(false, errors);
        }

        var valueStr = value.ToString() ?? "";
        if (set.Items.All(i => i.Value != valueStr))
        {
            var validValues = string.Join(", ", set.Items.Select(i => i.Value));
            errors.Add(new(def.AttributeName,
                $"值 '{valueStr}' 不在选项集中，有效值：{validValues}"));
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>解包数量值：支持 NumericValue 与裸数值</summary>
    private static (decimal? Value, Guid? UnitId) UnwrapNumeric(object value)
    {
        return value switch
        {
            NumericValue nv => (nv.Value, nv.UnitId),
            decimal d => (d, null),
            int i => (i, null),
            long l => (l, null),
            double db => ((decimal)db, null),
            string s when decimal.TryParse(s, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var parsed) => (parsed, null),
            _ => (null, null)
        };
    }

    /// <summary>把输入值归一化到属性基准单位</summary>
    private decimal NormalizeToBase(
        AttributeDefinition def, decimal value, Guid? inputUnitId)
    {
        if (def.UnitId is not { } baseUnitId || inputUnitId is null)
            return value;
        if (inputUnitId == baseUnitId)
            return value;

        return _converter.ToBase(value, inputUnitId.Value, baseUnitId);
    }

    private static void ValidateNumericRange(
        AttributeDefinition def, decimal value, List<ValidationError> errors)
    {
        if (def.ValidationRule is null) return;
        var rule = def.ValidationRule.RootElement;

        if (rule.TryGetProperty("min", out var min)
            && decimal.TryParse(min.ToString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var minVal)
            && value < minVal)
            errors.Add(new(def.AttributeName, $"不能小于 {minVal}"));

        if (rule.TryGetProperty("max", out var max)
            && decimal.TryParse(max.ToString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var maxVal)
            && value > maxVal)
            errors.Add(new(def.AttributeName, $"不能大于 {maxVal}"));
    }

    public bool TryConvert(string dataType, object value, out object? converted)
    {
        converted = null;
        try
        {
            converted = dataType switch
            {
                EavDataTypes.Int => Convert.ToInt64(value),
                EavDataTypes.Decimal => Convert.ToDecimal(value),
                EavDataTypes.Bool => Convert.ToBoolean(value),
                EavDataTypes.Datetime => value is DateTimeOffset dto
                    ? dto
                    : DateTimeOffset.Parse(value.ToString()!,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                EavDataTypes.Date => value is DateOnly d
                    ? d
                    : DateOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture),
                EavDataTypes.Time => value is TimeOnly t
                    ? t
                    : TimeOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture),
                EavDataTypes.String => value.ToString(),
                EavDataTypes.Json => value is JsonDocument jd
                    ? jd
                    : JsonDocument.Parse(JsonSerializer.Serialize(value)),
                EavDataTypes.File => value is JsonDocument fd
                    ? fd
                    : JsonDocument.Parse(JsonSerializer.Serialize(value)),
                _ => value.ToString()
            };
            return converted is not null;
        }
        catch { return false; }
    }

    private static void ValidateRule(
        AttributeDefinition def, object? value, List<ValidationError> errors)
    {
        var rule = def.ValidationRule!.RootElement;

        switch (def.DataType)
        {
            case EavDataTypes.Date when value is DateOnly dt:
                if (rule.TryGetProperty("minDate", out var minD)
                    && dt < DateOnly.Parse(minD.GetString()!))
                    errors.Add(new(def.AttributeName, "日期超出范围"));
                if (rule.TryGetProperty("maxDate", out var maxD)
                    && dt > DateOnly.Parse(maxD.GetString()!))
                    errors.Add(new(def.AttributeName, "日期超出范围"));
                break;

            case EavDataTypes.Time when value is TimeOnly tm:
                if (rule.TryGetProperty("minTime", out var minT)
                    && tm < TimeOnly.Parse(minT.GetString()!))
                    errors.Add(new(def.AttributeName, "时间超出范围"));
                if (rule.TryGetProperty("maxTime", out var maxT)
                    && tm > TimeOnly.Parse(maxT.GetString()!))
                    errors.Add(new(def.AttributeName, "时间超出范围"));
                break;

            case EavDataTypes.String when value is string str:
                if (rule.TryGetProperty("regex", out var regex)
                    && !Regex.IsMatch(str, regex.GetString()!))
                    errors.Add(new(def.AttributeName,
                        rule.TryGetProperty("message", out var m)
                            ? m.GetString()! : "格式不正确"));
                break;
        }
    }
}

public class EavValidationException : Exception
{
    public List<ValidationError> Errors { get; }
    public EavValidationException(List<ValidationError> errors)
        : base("EAV validation failed") => Errors = errors;
}
```

## 文件 37/41 TreeGraph.Api/Services/EavWriteService.cs

```csharp
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Services;

/// <summary>写入服务：验证 -> 插入/更新/删除 -> 审计（同一事务提交）</summary>
public class EavWriteService
{
    private readonly EavDbContext _db;
    private readonly IAttributeCache _attrCache;
    private readonly EavValidationService _validator;
    private readonly CompositeValueService _composite;
    private readonly IUnitCache _unitCache;
    private readonly UnitConverter _converter;
    private readonly IOptionSetCache _optionSetCache;

    public EavWriteService(
        EavDbContext db,
        IAttributeCache attrCache,
        EavValidationService validator,
        CompositeValueService composite,
        IUnitCache unitCache,
        UnitConverter converter,
        IOptionSetCache optionSetCache)
    {
        _db = db;
        _attrCache = attrCache;
        _validator = validator;
        _composite = composite;
        _unitCache = unitCache;
        _converter = converter;
        _optionSetCache = optionSetCache;
    }

    /// <summary>
    /// 保存实体属性（全量替换语义）。
    ///
    /// 语义：
    ///   - values 中出现的键：验证 + 写入
    ///   - values 中值为 null 的键：删除该属性
    ///   - values 中未出现的键（但 catalog 中定义的属性）：**删除**
    ///
    /// ★ 乐观锁：expectedUpdatedAt 非 null 时，比对 DB 中该实体的
    ///   max(AttributeValue.UpdatedAt)。**用 UtcDateTime 比较**，
    ///   避免客户端回传时携带不同 offset（如本地 +08:00）导致误判。
    ///
    /// ★ 未知属性：values 中出现 catalog 未定义的属性名时返回 400，
    ///   而非静默丢弃（防客户端拼写错误导致数据丢失）。
    /// </summary>
    public Task SaveAsync(
        string entityId, string entityType,
        Dictionary<string, object?> values,
        string changedBy, string? correlationId = null,
        CancellationToken ct = default,
        DateTimeOffset? expectedUpdatedAt = null)
        => SaveCoreAsync(entityId, entityType, values,
            changedBy, correlationId, ct, expectedUpdatedAt,
            fullReplace: true);

    /// <summary>
    /// 部分更新实体属性（PATCH 语义）。
    ///
    /// 语义：
    ///   - values 中出现的键：验证 + 写入
    ///   - values 中值为 null 的键：删除该属性
    ///   - values 中未出现的键：**保持原值不动**
    ///
    /// 与 SaveAsync 的差异仅在"未提供的属性"处理上。
    /// 乐观锁、未知属性检查、验证流程与 SaveAsync 完全一致。
    /// </summary>
    public Task PatchAsync(
        string entityId, string entityType,
        Dictionary<string, object?> values,
        string changedBy, string? correlationId = null,
        CancellationToken ct = default,
        DateTimeOffset? expectedUpdatedAt = null)
        => SaveCoreAsync(entityId, entityType, values,
            changedBy, correlationId, ct, expectedUpdatedAt,
            fullReplace: false);

    /// <summary>
    /// Save / Patch 的公共核心逻辑。
    ///
    /// - fullReplace = true  → 未提供的键（catalog 中定义）将被删除（PUT 语义）
    /// - fullReplace = false → 未提供的键保持不动（PATCH 语义）
    /// </summary>
    private async Task SaveCoreAsync(
        string entityId, string entityType,
        Dictionary<string, object?> values,
        string changedBy, string? correlationId,
        CancellationToken ct,
        DateTimeOffset? expectedUpdatedAt,
        bool fullReplace)
    {
        // ---- -1. Entity Id 格式校验（GUID 字符串）----
        if (!Guid.TryParse(entityId, out _))
            throw new EavValidationException(new List<ValidationError>
                { new("id", "Entity Id 必须是 GUID 格式") });

        var definitions = _attrCache.GetDefinitions(entityType)
            .ToDictionary(d => d.AttributeName);

        // ---- 0. 未知属性检查 ----
        var unknownKeys = values.Keys
            .Where(k => !definitions.ContainsKey(k))
            .ToList();
        if (unknownKeys.Count > 0)
        {
            throw new EavValidationException(unknownKeys
                .Select(k => new ValidationError(k, "未知属性"))
                .ToList());
        }

        // ---- 1. 乐观锁检测（用 UtcDateTime 语义比较）----
        if (expectedUpdatedAt is { } expected)
        {
            var currentMax = await _db.AttributeValues
                .Where(v => v.EntityType == entityType && v.EntityId == entityId)
                .MaxAsync(v => (DateTimeOffset?)v.UpdatedAt, ct);

            // ★ 修复：DateTimeOffset.!= 会比较 instant + offset，
            //   同一瞬间但 offset 不同会误判冲突。改用 UtcDateTime。
            if (currentMax is null
                || currentMax.Value.UtcDateTime != expected.UtcDateTime)
            {
                throw new EavConcurrencyException(
                    expected, currentMax ?? DateTimeOffset.MinValue);
            }
        }

        // ---- 2. 验证提供的值 ----
        var validationErrors = new List<ValidationError>();
        foreach (var def in definitions.Values)
        {
            values.TryGetValue(def.AttributeName, out var rawValue);

            // PATCH 模式下：未提供的键不参与必填检查
            var provided = values.ContainsKey(def.AttributeName);
            if (!fullReplace && !provided) continue;

            if (def.IsRequired && rawValue is null)
            {
                if (def.DataType == EavDataTypes.SingleChoice
                    && def.RefOptionSetId is { } sid
                    && _optionSetCache.GetSet(sid).Items.Any(i => i.IsDefault))
                    continue;

                validationErrors.Add(new(def.AttributeName, "必填字段未提供"));
                continue;
            }

            if (rawValue is null) continue;

            if (def.DataType == EavDataTypes.Composite && rawValue is DynamicCompositeValue cv)
            {
                var r = _composite.Validate(cv, def.RefCompositeTypeId!);
                if (!r.IsValid) validationErrors.AddRange(r.Errors);
            }
            else
            {
                var r = _validator.ValidateBaseValue(def, rawValue);
                if (!r.IsValid) validationErrors.AddRange(r.Errors);
            }
        }
        if (validationErrors.Count > 0)
            throw new EavValidationException(validationErrors);

        var existing = await _db.AttributeValues
            .Where(v => v.EntityType == entityType && v.EntityId == entityId)
            .ToListAsync(ct);
        var existingMap = existing.ToDictionary(v => v.AttributeId);

        var audits = new List<AttributeAuditLog>();
        var now = DateTimeOffset.UtcNow;

        // ---- 3. 处理显式提供的值 ----
        foreach (var (name, rawValue) in values)
        {
            if (!definitions.TryGetValue(name, out var def)) continue;

            var valueToWrite = rawValue;
            if (def.DataType == EavDataTypes.SingleChoice && valueToWrite is null
                && def.RefOptionSetId is { } osid)
            {
                valueToWrite = _optionSetCache.GetSet(osid)
                    .Items.FirstOrDefault(i => i.IsDefault)?.Value;
            }

            existingMap.TryGetValue(def.AttributeId, out var existingValue);

            // 显式 null 走删除路径
            if (valueToWrite is null)
            {
                if (existingValue is not null)
                {
                    _db.AttributeValues.Remove(existingValue);
                    audits.Add(CreateAudit(entityId, entityType, def,
                        SerializeForAudit(existingValue, def), null, "Delete",
                        changedBy, correlationId, now));
                }
                continue;
            }

            if (existingValue is null)
            {
                existingValue = new AttributeValue
                {
                    // ValueId 为空字符串 → 触发 DB DEFAULT gen_random_uuid()（HasSentinel 语义）
                    EntityId = entityId,
                    EntityType = entityType,
                    AttributeId = def.AttributeId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                SetTypedValue(existingValue, def, valueToWrite);
                _db.AttributeValues.Add(existingValue);
                audits.Add(CreateAudit(entityId, entityType, def,
                    null, SerializeForAudit(existingValue, def),
                    "Insert", changedBy, correlationId, now));
            }
            else
            {
                var oldSerialized = SerializeForAudit(existingValue, def);
                ClearValueColumns(existingValue);
                SetTypedValue(existingValue, def, valueToWrite);
                existingValue.UpdatedAt = now;

                var newSerialized = SerializeForAudit(existingValue, def);
                if (oldSerialized != newSerialized)
                {
                    audits.Add(CreateAudit(entityId, entityType, def,
                        oldSerialized, newSerialized, "Update",
                        changedBy, correlationId, now));
                }
            }
        }

        // ---- 4. PUT 语义：未提供的属性 → 删除 ----
        if (fullReplace)
        {
            foreach (var def in definitions.Values)
            {
                if (!values.ContainsKey(def.AttributeName)
                    && existingMap.TryGetValue(def.AttributeId, out var oldVal))
                {
                    _db.AttributeValues.Remove(oldVal);
                    audits.Add(CreateAudit(entityId, entityType, def,
                        SerializeForAudit(oldVal, def), null, "Delete",
                        changedBy, correlationId, now));
                }
            }
        }

        _db.AttributeAuditLogs.AddRange(audits);
        await _db.SaveChangesAsync(ct);
    }

    private void SetTypedValue(
        AttributeValue target, AttributeDefinition def, object? value)
    {
        if (value is null) return;

        switch (def.DataType)
        {
            case EavDataTypes.String:
                target.ValueString = value.ToString();
                break;
            case EavDataTypes.SingleChoice:
                target.ValueString = value.ToString();
                break;
            case EavDataTypes.Int:
            case EavDataTypes.Decimal:
            {
                var (numericValue, inputUnitId) = UnwrapNumeric(value);
                if (numericValue is null)
                    throw new InvalidOperationException("数值格式错误");

                decimal normalized = numericValue.Value;
                if (def.UnitId is { } baseUnitId && inputUnitId is { } iu)
                {
                    normalized = _converter.ToBase(numericValue.Value, iu, baseUnitId);
                    target.UnitId = iu;
                }
                else
                {
                    target.UnitId = null;
                }

                if (def.DataType == EavDataTypes.Int)
                    target.ValueInt = (long)normalized;
                else
                    target.ValueDecimal = normalized;
                break;
            }
            case EavDataTypes.Bool:
                target.ValueBool = Convert.ToBoolean(value);
                break;
            case EavDataTypes.Datetime:
                target.ValueDatetime = value is DateTimeOffset dto
                    ? dto
                    : DateTimeOffset.Parse(value.ToString()!,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                break;
            case EavDataTypes.Date:
                target.ValueDateOnly = value is DateOnly d
                    ? d
                    : DateOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture);
                break;
            case EavDataTypes.Time:
                target.ValueTime = value is TimeOnly t
                    ? t
                    : TimeOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture);
                break;
            case EavDataTypes.File:
                target.ValueFileMeta = value is JsonDocument fd
                    ? fd
                    : JsonDocument.Parse(JsonSerializer.Serialize(value));
                break;
            case EavDataTypes.Json:
                target.ValueJsonb = value is JsonDocument jd
                    ? jd
                    : JsonDocument.Parse(JsonSerializer.Serialize(value));
                break;
            case EavDataTypes.Composite:
                if (value is not DynamicCompositeValue cv)
                    throw new InvalidOperationException("composite 值类型错误");
                target.ValueJsonb = _composite.Serialize(cv, def.RefCompositeTypeId!);
                break;
        }
    }

    private static void ClearValueColumns(AttributeValue v)
    {
        v.ValueString = null;
        v.ValueInt = null;
        v.ValueDecimal = null;
        v.ValueBool = null;
        v.ValueDatetime = null;
        v.ValueDateOnly = null;
        v.ValueTime = null;
        v.ValueFileMeta = null;
        v.ValueJsonb = null;
        v.UnitId = null;
    }

    private static (decimal? Value, Guid? UnitId) UnwrapNumeric(object value)
    {
        return value switch
        {
            NumericValue nv => (nv.Value, nv.UnitId),
            decimal d => (d, null),
            int i => (i, null),
            long l => (l, null),
            double db => ((decimal)db, null),
            string s when decimal.TryParse(s, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var parsed) => (parsed, null),
            _ => (null, null)
        };
    }

    private string? SerializeForAudit(AttributeValue v, AttributeDefinition def)
    {
        return def.DataType switch
        {
            EavDataTypes.String => v.ValueString,
            EavDataTypes.SingleChoice => FormatSingleChoice(v.ValueString, def),
            EavDataTypes.Int when v.ValueInt is { } i => FormatNumeric(i, v.UnitId),
            EavDataTypes.Decimal when v.ValueDecimal is { } d => FormatNumeric(d, v.UnitId),
            EavDataTypes.Int => null,
            EavDataTypes.Decimal => null,
            EavDataTypes.Bool => v.ValueBool?.ToString(),
            EavDataTypes.Datetime => v.ValueDatetime?.ToString("o"),
            EavDataTypes.Date => v.ValueDateOnly?.ToString("yyyy-MM-dd"),
            EavDataTypes.Time => v.ValueTime?.ToString("HH:mm:ss"),
            EavDataTypes.File => v.ValueFileMeta?.RootElement.GetRawText(),
            EavDataTypes.Json => v.ValueJsonb?.RootElement.GetRawText(),
            EavDataTypes.Composite => v.ValueJsonb?.RootElement.GetRawText(),
            _ => null
        };
    }

    private string FormatSingleChoice(string? value, AttributeDefinition def)
    {
        if (value is null) return "";
        if (def.RefOptionSetId is not { } sid) return value;

        try
        {
            var item = _optionSetCache.GetSet(sid).Items
                .FirstOrDefault(i => i.Value == value);
            return item is null ? value : $"{value} ({item.Label})";
        }
        catch (KeyNotFoundException)
        {
            return value;
        }
    }

    private string FormatNumeric(decimal value, Guid? originalUnitId)
    {
        if (originalUnitId is not { } uid)
            return value.ToString(CultureInfo.InvariantCulture);

        try
        {
            var unit = _unitCache.Get(uid);
            return $"{value} {unit.Symbol}";
        }
        catch (InvalidOperationException)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static AttributeAuditLog CreateAudit(
        string entityId, string entityType, AttributeDefinition def,
        string? oldValue, string? newValue, string changeType,
        string changedBy, string? correlationId, DateTimeOffset now)
    {
        return new AttributeAuditLog
        {
            // AuditId 为空字符串 → 触发 DB DEFAULT gen_random_uuid()（HasSentinel 语义）
            EntityId = entityId,
            EntityType = entityType,
            AttributeId = def.AttributeId,
            AttributeName = def.AttributeName,
            OldValue = oldValue,
            NewValue = newValue,
            ChangeType = changeType,
            ChangedBy = changedBy,
            ChangedAt = now,
            CorrelationId = correlationId
        };
    }

    /// <summary>删除实体（物理删除所有属性值 + 自定义表行，写审计）。</summary>
    public async Task<bool> DeleteEntityAsync(
        string entityId, string entityType,
        string changedBy, string? correlationId = null,
        CancellationToken ct = default)
    {
        var hasValues = await _db.AttributeValues
            .AnyAsync(v => v.EntityType == entityType && v.EntityId == entityId, ct);

        var hasRows = await _db.CustomTableRows
            .AnyAsync(r => r.ParentEntityType == entityType
                        && r.ParentEntityId == entityId, ct);

        if (!hasValues && !hasRows)
            return false;

        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var now = DateTimeOffset.UtcNow;

            var values = await _db.AttributeValues
                .Where(v => v.EntityType == entityType && v.EntityId == entityId)
                .ToListAsync(ct);

            if (values.Count > 0)
            {
                var definitions = _attrCache.GetDefinitions(entityType)
                    .ToDictionary(d => d.AttributeId);

                var audits = new List<AttributeAuditLog>(values.Count);
                foreach (var v in values)
                {
                    if (!definitions.TryGetValue(v.AttributeId, out var def)) continue;

                    audits.Add(new AttributeAuditLog
                    {
                        EntityId = entityId,
                        EntityType = entityType,
                        AttributeId = def.AttributeId,
                        AttributeName = def.AttributeName,
                        OldValue = SerializeForAudit(v, def),
                        NewValue = null,
                        ChangeType = "Delete",
                        ChangedBy = changedBy,
                        ChangedAt = now,
                        CorrelationId = correlationId
                    });
                }
                _db.AttributeAuditLogs.AddRange(audits);
            }

            _db.AttributeValues.RemoveRange(values);

            var rows = await _db.CustomTableRows
                .Where(r => r.ParentEntityType == entityType
                         && r.ParentEntityId == entityId)
                .ToListAsync(ct);
            _db.CustomTableRows.RemoveRange(rows);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        return true;
    }

    /// <summary>
    /// 批量删除多个实体（硬删除：属性值 + 自定义表行）。
    ///
    /// 语义：
    ///   - 请求的 ID 去重后处理
    ///   - 不存在的 ID 计入 NotFound（不报错）
    ///   - 单个事务内完成所有删除 + 审计
    ///   - 与 DeleteEntityAsync 共享"删除 = 删两张表 + 写审计"的策略
    /// </summary>
    public async Task<BatchDeleteResult> DeleteEntitiesAsync(
        IReadOnlyList<string> entityIds,
        string entityType,
        string changedBy,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        if (entityIds.Count == 0)
            return new BatchDeleteResult(
                Array.Empty<string>(), Array.Empty<string>(), 0);

        var requested = entityIds.Distinct().ToList();

        // 存在性：任一表命中即认为存在
        var hasValues = await _db.AttributeValues
            .Where(v => v.EntityType == entityType && requested.Contains(v.EntityId))
            .Select(v => v.EntityId)
            .Distinct()
            .ToListAsync(ct);

        var hasRows = await _db.CustomTableRows
            .Where(r => r.ParentEntityType == entityType
                     && requested.Contains(r.ParentEntityId))
            .Select(r => r.ParentEntityId)
            .Distinct()
            .ToListAsync(ct);

        var existingSet = hasValues.Union(hasRows).ToHashSet();
        var toDelete = requested.Where(id => existingSet.Contains(id)).ToList();
        var notFound = requested.Where(id => !existingSet.Contains(id)).ToList();

        if (toDelete.Count == 0)
            return new BatchDeleteResult(
                Array.Empty<string>(), notFound, 0);

        var strategy = _db.Database.CreateExecutionStrategy();
        int totalAttributesDeleted = 0;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var now = DateTimeOffset.UtcNow;

            // 1. 加载待删属性值（用于审计 + 计数）
            var values = await _db.AttributeValues
                .Where(v => v.EntityType == entityType
                         && toDelete.Contains(v.EntityId))
                .ToListAsync(ct);

            // 2. 写审计
            if (values.Count > 0)
            {
                var definitions = _attrCache.GetDefinitions(entityType)
                    .ToDictionary(d => d.AttributeId);

                var audits = new List<AttributeAuditLog>(values.Count);
                foreach (var v in values)
                {
                    if (!definitions.TryGetValue(v.AttributeId, out var def)) continue;
                    audits.Add(new AttributeAuditLog
                    {
                        EntityId = v.EntityId,
                        EntityType = entityType,
                        AttributeId = def.AttributeId,
                        AttributeName = def.AttributeName,
                        OldValue = SerializeForAudit(v, def),
                        NewValue = null,
                        ChangeType = "Delete",
                        ChangedBy = changedBy,
                        ChangedAt = now,
                        CorrelationId = correlationId
                    });
                }
                _db.AttributeAuditLogs.AddRange(audits);
            }

            totalAttributesDeleted = values.Count;

            // 3. 删除属性值
            _db.AttributeValues.RemoveRange(values);

            // 4. 删除自定义表行
            var rows = await _db.CustomTableRows
                .Where(r => r.ParentEntityType == entityType
                         && toDelete.Contains(r.ParentEntityId))
                .ToListAsync(ct);
            _db.CustomTableRows.RemoveRange(rows);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        return new BatchDeleteResult(toDelete, notFound, totalAttributesDeleted);
    }
}

/// <summary>
/// 批量删除结果。
/// </summary>
public record BatchDeleteResult(
    IReadOnlyList<string> Deleted,
    IReadOnlyList<string> NotFound,
    int TotalAttributesDeleted);

/// <summary>
/// 并发冲突异常。EavWriteService.SaveAsync 在乐观锁检测失败时抛出。
/// EavController 捕获后返回 409 Conflict + 当前 UpdatedAt。
/// </summary>
public class EavConcurrencyException : Exception
{
    public DateTimeOffset ExpectedUpdatedAt { get; }
    public DateTimeOffset CurrentUpdatedAt { get; }

    public EavConcurrencyException(
        DateTimeOffset expectedUpdatedAt, DateTimeOffset currentUpdatedAt)
        : base("并发冲突：实体已被其他用户修改")
    {
        ExpectedUpdatedAt = expectedUpdatedAt;
        CurrentUpdatedAt = currentUpdatedAt;
    }
}
```

## 文件 38/41 TreeGraph.Api/Services/NumericValue.cs

```csharp
namespace TreeGraph.Api.Services;

/// <summary>
/// 数量类型运行时值：数值 + 单位。
/// UnitId 为 null 时表示按属性基准单位输入。
/// </summary>
public readonly record struct NumericValue(decimal Value, Guid? UnitId = null);
```

## 文件 39/41 TreeGraph.Api/Services/OptionSetCache.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;

namespace TreeGraph.Api.Services;

public interface IOptionSetCache
{
    OptionSet GetSet(string optionSetId);
    List<OptionSet> GetAll(string? entityType = null, bool includeDeleted = false);
    void Invalidate(string optionSetId);
}

/// <summary>选项集缓存（单例，含未删除选项项）</summary>
public class OptionSetCache : IOptionSetCache
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public OptionSetCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// 按 ID 取选项集。**不过滤集合级 IsDeleted**：
    /// 历史数据里已存有该集合的 Value，读取时需要 Label 做降级显示。
    /// 集合级 IsDeleted 仅用于管理页列表过滤。
    /// </summary>
    public OptionSet GetSet(string optionSetId)
    {
        return _cache.GetOrCreate($"eav:oset:{optionSetId}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
            var set = db.OptionSets
                .Include(s => s.Items.Where(i => !i.IsDeleted))
                .AsNoTracking()
                .FirstOrDefault(s => s.OptionSetId == optionSetId)
                ?? throw new KeyNotFoundException($"选项集不存在: {optionSetId}");
            set.Items = set.Items.OrderBy(i => i.DisplayOrder).ToList();
            return set;
        })!;
    }

    /// <summary>
    /// 列出选项集。
    /// ★ includeDeleted = true 时返回软删除的集合（管理页恢复入口）。
    /// </summary>
    public List<OptionSet> GetAll(string? entityType = null, bool includeDeleted = false)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();

        var query = db.OptionSets.AsQueryable();
        if (!includeDeleted) query = query.Where(s => !s.IsDeleted);
        if (entityType is not null)
            query = query.Where(s => s.EntityType == entityType || s.EntityType == "Shared");

        var ids = query.OrderBy(s => s.OptionSetId)
            .AsNoTracking()
            .Select(s => s.OptionSetId)
            .ToList();

        var result = new List<OptionSet>(ids.Count);
        foreach (var id in ids)
        {
            try { result.Add(GetSet(id)); }
            catch (KeyNotFoundException) { /* 已删除，跳过 */ }
        }
        return result;
    }

    public void Invalidate(string optionSetId)
        => _cache.Remove($"eav:oset:{optionSetId}");
}

/// <summary>单选值运行时表示：Value 是存储值，Label 是展示名</summary>
public sealed record SingleChoiceValue(string Value, string Label);
```

## 文件 40/41 TreeGraph.Api/Services/UnitCache.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TreeGraph.Api.Data;
using TreeGraph.Api.Entities;

namespace TreeGraph.Api.Services;

public interface IUnitCache
{
    Unit Get(Guid id);
    IReadOnlyList<Unit> GetAll();
    IReadOnlyList<Unit> GetByCategory(string category);
    Unit? GetBaseUnit(string category);
    void Invalidate();
}

/// <summary>单位缓存（单例，单位表几乎不变）</summary>
public class UnitCache : IUnitCache
{
    private const string CacheKey = "eav:units:all";
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public UnitCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public IReadOnlyList<Unit> GetAll()
    {
        return _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            entry.SlidingExpiration = TimeSpan.FromMinutes(20);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EavDbContext>();
            return db.Units
                .Where(u => !u.IsDeleted)
                .OrderBy(u => u.Category)
                .ThenBy(u => u.DisplayOrder)
                .AsNoTracking()
                .ToList();
        })!;
    }

    public Unit Get(Guid id)
    {
        var unit = GetAll().FirstOrDefault(u => u.Id == id);
        if (unit is null)
            throw new InvalidOperationException($"单位不存在: {id}");
        return unit;
    }

    public IReadOnlyList<Unit> GetByCategory(string category)
        => GetAll().Where(u => u.Category == category).ToList();

    public Unit? GetBaseUnit(string category)
        => GetAll().FirstOrDefault(u => u.Category == category && u.IsBaseUnit);

    public void Invalidate() => _cache.Remove(CacheKey);
}
```

## 文件 41/41 TreeGraph.Api/Services/UnitConverter.cs

```csharp
using TreeGraph.Api.Entities;

namespace TreeGraph.Api.Services;

/// <summary>单位换算服务：目标值 = 源值 × 源单位系数 ÷ 目标单位系数</summary>
public class UnitConverter
{
    private readonly IUnitCache _unitCache;

    public UnitConverter(IUnitCache unitCache) => _unitCache = unitCache;

    /// <summary>把值从 fromUnit 换算到 toUnit（同分类）</summary>
    public decimal Convert(decimal value, Guid fromUnitId, Guid toUnitId)
    {
        if (fromUnitId == toUnitId) return value;

        var from = _unitCache.Get(fromUnitId);
        var to = _unitCache.Get(toUnitId);

        if (from.Category != to.Category)
            throw new InvalidOperationException(
                $"跨分类换算不允许：{from.Category} → {to.Category}");

        return value * from.ToBaseFactor / to.ToBaseFactor;
    }

    /// <summary>把值换算到属性基准单位</summary>
    public decimal ToBase(decimal value, Guid inputUnitId, Guid baseUnitId)
        => Convert(value, inputUnitId, baseUnitId);

    /// <summary>从基准单位还原到指定单位</summary>
    public decimal FromBase(decimal baseValue, Guid baseUnitId, Guid targetUnitId)
        => Convert(baseValue, baseUnitId, targetUnitId);
}
```


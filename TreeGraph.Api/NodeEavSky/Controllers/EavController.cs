using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.Infrastructure;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.NodeEavSky;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Controllers;

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
    private readonly IOptionSetCache _optionSetCache;
    private readonly TreeGraphDbContext _db;
    private readonly JsonSerializerOptions _jsonOptions;

    public EavController(
        EavWriteService write, EavReadService read, EavQueryService query,
        CustomTableQueryService tableQuery,
        IAttributeCache attrCache, ICompositeTypeCache compositeCache,
        IOptionSetCache optionSetCache, TreeGraphDbContext db,
        IOptions<JsonOptions> jsonOptions)
    {
        _write = write;
        _read = read;
        _query = query;
        _tableQuery = tableQuery;
        _attrCache = attrCache;
        _compositeCache = compositeCache;
        _optionSetCache = optionSetCache;
        _db = db;
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
    public async Task<IActionResult> GetSchema(string entityType, CancellationToken ct)
    {
        var defs = _attrCache.GetDefinitions(entityType);

        // 单位名称投影（unit_trees 节点，仅作展示；含组合内字段的单位）
        var unitIds = defs.Select(d => d.UnitId)
            .Where(id => id is not null).Select(id => id!)
            .ToList();

        foreach (var d in defs.Where(d => d.RefCompositeTypeId is not null))
            CollectFieldUnitIds(d.RefCompositeTypeId!, unitIds);

        unitIds = unitIds.Distinct().ToList();
        var unitNames = unitIds.Count == 0
            ? new Dictionary<string, string>()
            : await _db.UnitTrees
                .Where(u => unitIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var schema = defs.Select(d => ToSchemaDto(d, unitNames)).ToList();
        return Ok(new { EntityType = entityType, Attributes = schema });
    }

    [HttpGet("entities/{id}")]
    public async Task<IActionResult> Get(
        string id, string entityType,
        CancellationToken ct)
    {
        var entity = await _read.LoadAsync(id, entityType, ct);
        return Ok(ToDto(entity));
    }

    /// <summary>
    /// PUT 全量替换实体属性（支持乐观锁）。
    ///
    /// ⚠️ 删除语义（务必注意）：本端点为【全量替换】——
    ///   - values 中出现的属性：验证 + 写入
    ///   - values 中值为 null 的属性：删除
    ///   - values 中【未出现】的属性（catalog 中已定义）：一并删除！
    /// 只想更新部分属性时【必须改用 PATCH】，否则未提交属性的既有值将丢失。
    ///
    /// ★ #3：乐观锁。客户端从 GET 拿到的 UpdatedAt 通过 header 传回：
    ///   X-Expected-Updated-At: 2026-10-01T10:30:00.0000000+00:00
    ///
    /// 服务端比对 DB 中最新 UpdatedAt，不符则 409 Conflict 并返回最新值。
    /// header 缺失时跳过冲突检测（向后兼容）。
    /// </summary>
    [HttpPut("entities/{id}")]
    [IdempotentWrite]
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
    [IdempotentWrite]
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
    [IdempotentWrite]
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

    /// <summary>递归收集组合类型字段上的单位 Id（含嵌套组合）。</summary>
    private void CollectFieldUnitIds(string compositeTypeId, List<string> unitIds)
    {
        var ct = _compositeCache.GetType(compositeTypeId);
        foreach (var f in ct.Fields.Where(f => !f.IsDeleted))
        {
            if (f.UnitId is { } uid) unitIds.Add(uid);
            if (f.DataType == EavDataTypes.Composite && f.RefCompositeTypeId is { } rid)
                CollectFieldUnitIds(rid, unitIds);
        }
    }

    private AttributeSchemaDto ToSchemaDto(
        AttributeDefinition d, IReadOnlyDictionary<string, string> unitNames)
    {
        CompositeTypeSchemaDto? composite = null;
        if (d.RefCompositeTypeId is not null)
            composite = BuildCompositeSchema(d.RefCompositeTypeId, unitNames);

        string? unitName = null;
        if (d.UnitId is { } uid)
            unitNames.TryGetValue(uid, out unitName);

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
            composite, d.UnitId, unitName, optionSet,
            d.RefTableDefinitionId, d.IsRecordMode);
    }

    /// <summary>
    /// 递归构造组合类型 Schema。
    ///
    /// ★ #4：字段的 UnitId / UnitName 一并下放（仅 decimal 字段可能非空）。
    /// </summary>
    private CompositeTypeSchemaDto BuildCompositeSchema(
        string compositeTypeId, IReadOnlyDictionary<string, string> unitNames)
    {
        var ct = _compositeCache.GetType(compositeTypeId);

        var fields = ct.Fields
            .Where(f => !f.IsDeleted)
            .OrderBy(f => f.DisplayOrder)
            .Select(f => BuildFieldSchema(f, unitNames))
            .ToList();

        return new CompositeTypeSchemaDto(ct.TypeName, fields);
    }

    private CompositeFieldSchemaDto BuildFieldSchema(
        CompositeFieldDefinition f, IReadOnlyDictionary<string, string> unitNames)
    {
        // ★ #4：单位名称投影
        string? unitName = null;
        if (f.UnitId is { } uid)
            unitNames.TryGetValue(uid, out unitName);

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
                ? BuildCompositeSchema(rid, unitNames)
                : null,
            f.ValidationRule?.RootElement.Clone(),
            f.AllowedValues?.RootElement.Clone(),
            f.UnitId,
            unitName,
            optionSet);   // ★ #8
    }

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
            EavDataTypes.Location => EavRequestBodyConverter.ParseLocation(elem),
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
            string? unitId = elem.TryGetProperty("unitId", out var u)
                             && u.ValueKind == JsonValueKind.String
                ? u.GetString()
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
            string? unitId = elem.TryGetProperty("unitId", out var u)
                             && u.ValueKind == JsonValueKind.String
                ? u.GetString()
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

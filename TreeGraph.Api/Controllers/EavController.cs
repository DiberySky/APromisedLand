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

    /// <summary>★ 修复 P0-4：把 DynamicEntity 转成契约 DTO（Properties 为 JsonElement 字典）</summary>
    private DynamicEntityDto ToDto(DynamicEntity entity)
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var (k, v) in entity.Properties)
        {
            dict[k] = v is null
                ? JsonDocument.Parse("null").RootElement.Clone()
                : JsonSerializer.SerializeToElement(v, v.GetType(), _jsonOptions);
        }
        return new DynamicEntityDto(entity.EntityId, entity.EntityType, dict);
    }

    [HttpGet("schema")]
    public IActionResult GetSchema(string entityType)
    {
        var defs = _attrCache.GetDefinitions(entityType);
        var schema = defs.Select(ToSchemaDto).ToList();
        return Ok(new { EntityType = entityType, Attributes = schema });
    }

    [HttpGet("entities/{id:long}")]
    public async Task<IActionResult> Get(
        long id, string entityType,
        [FromQuery] string? unit,
        CancellationToken ct)
    {
        var originalUnits = unit == "original";
        var entity = await _read.LoadAsync(id, entityType, originalUnits, ct);
        return Ok(ToDto(entity));
    }

    [HttpPut("entities/{id:long}")]
    public async Task<IActionResult> Put(
        long id, string entityType,
        [FromBody] Dictionary<string, JsonElement> values,
        CancellationToken ct)
    {
        try
        {
            var typedValues = ConvertJsonValues(entityType, values);
            await _write.SaveAsync(id, entityType, typedValues,
                User.Identity?.Name ?? "system", HttpContext.TraceIdentifier, ct);
            return NoContent();
        }
        catch (EavValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
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
            // ★ 修复 P0-4：转成 DTO 返回
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

    [HttpGet("entities/{id:long}/history")]
    public async Task<IActionResult> History(
        long id, string entityType,
        [FromQuery] DateTimeOffset? from,
        CancellationToken ct)
    {
        var history = await _read.GetHistoryAsync(id, entityType, from, ct);
        return Ok(history);
    }

    // ---------- Schema 映射 ----------

    private AttributeSchemaDto ToSchemaDto(AttributeDefinition d)
    {
        CompositeTypeSchemaDto? composite = null;
        if (d.RefCompositeTypeId is not null)
            composite = BuildCompositeSchema(d.RefCompositeTypeId.Value);

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
            catch (InvalidOperationException)
            {
                // 单位已被删除：schema 中返回 null，不阻塞其它属性
            }
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
            d.IsRequired, d.IsSearchable, d.IsSortable, d.IsMultiValue, d.DisplayOrder,
            d.AllowedValues?.RootElement.Clone(),
            d.ValidationRule?.RootElement.Clone(),
            composite, unit, availableUnits, optionSet,
            d.RefTableDefinitionId);
    }

    private CompositeTypeSchemaDto BuildCompositeSchema(long compositeTypeId)
    {
        var ct = _compositeCache.GetType(compositeTypeId);

        var fields = ct.Fields
            .Where(f => !f.IsDeleted)
            .OrderBy(f => f.DisplayOrder)
            .Select(f => new CompositeFieldSchemaDto(
                f.FieldName, f.DisplayName, f.DataType,
                f.IsArray, f.IsRequired, f.IsSearchable, f.DisplayOrder,
                f.RefCompositeTypeId,
                f.DataType == EavDataTypes.Composite && f.RefCompositeTypeId is { } rid
                    ? BuildCompositeSchema(rid)
                    : null))
            .ToList();

        return new CompositeTypeSchemaDto(ct.TypeName, fields);
    }

    private static UnitSchemaDto ToUnitDto(Unit u)
        => new(u.Id, u.Category, u.Name, u.Symbol, u.IsBaseUnit);

    // ---------- 请求体 JsonElement -> 强类型值 ----------

    private Dictionary<string, object?> ConvertJsonValues(
        string entityType, Dictionary<string, JsonElement> values)
    {
        var defs = _attrCache.GetDefinitions(entityType).ToDictionary(d => d.AttributeName);
        var result = new Dictionary<string, object?>();

        foreach (var (name, elem) in values)
        {
            if (!defs.TryGetValue(name, out var def)) continue;
            if (def.DataType == EavDataTypes.Table) continue;
            result[name] = ConvertJsonElement(elem, def);
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
        return CompositeFromDoc(doc, def.RefCompositeTypeId!.Value);
    }

    private DynamicCompositeValue CompositeFromDoc(JsonDocument doc, long compositeTypeId)
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

    private object? JsonElementToValue(JsonElement elem, CompositeFieldDefinition field)
    {
        if (elem.ValueKind == JsonValueKind.Null) return null;

        if (field.DataType == EavDataTypes.Composite)
        {
            using var nested = JsonDocument.Parse(elem.GetRawText());
            return CompositeFromDoc(nested, field.RefCompositeTypeId!.Value);
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

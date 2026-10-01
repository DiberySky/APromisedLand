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

    public async Task<PagedResult<long>> FilterEntityIdsAsync(
        EavQueryRequest request, CancellationToken ct = default)
    {
        var definitions = _attrCache.GetDefinitions(request.EntityType)
            .ToDictionary(d => d.AttributeName);

        IQueryable<long>? entityQuery = null;

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

        var ids = await entityQuery
            .OrderBy(id => id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return new PagedResult<long>
        {
            Items = ids,
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize
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

    private IQueryable<long> BuildSingleFilterQuery(
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
            "in" => q.Where(x => x.ValueInt != null && InLongs(f.Value).Contains(x.ValueInt.Value)),
            _ => throw new NotSupportedException($"不支持的运算符: {f.Operator}")
        };
    }

    private IQueryable<AttributeValue> ApplyDecimalFilter(
        IQueryable<AttributeValue> q, AttributeDefinition def, AttributeFilter f)
    {
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
        // ★ 修复 P0-1：JsonElement 无法被 Convert.ToBoolean 处理
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

    private IQueryable<long> BuildCompositeFilterQuery(
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
            query = fieldDef.DataType switch
            {
                EavDataTypes.Int => filter.Operator switch
                {
                    // ★ 修复 P0-1：JsonElement 无法被 Convert.ToInt64 处理
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
                    // ★ 修复 P0-1
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
        else
        {
            var nestedName = fieldPath[1];
            query = fieldDef.DataType switch
            {
                EavDataTypes.Int => filter.Operator switch
                {
                    // ★ 修复 P0-1
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

        return query.Select(v => v.EntityId).Distinct();
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
        long typeId = def.RefCompositeTypeId.Value;

        foreach (var segment in path)
        {
            var typeDef = _compositeCache.GetType(typeId);
            current = typeDef.Fields
                .FirstOrDefault(f => f.FieldName == segment && !f.IsDeleted);
            if (current is null) return null;

            if (current.DataType == EavDataTypes.Composite
                && current.RefCompositeTypeId is not null)
                typeId = current.RefCompositeTypeId.Value;
        }

        return current;
    }

    // ---------- 工具方法 ----------

    private static long[] InLongs(object? value)
    {
        if (value is JsonElement e && e.ValueKind == JsonValueKind.Array)
            return e.EnumerateArray().Select(x => x.GetInt64()).ToArray();
        if (value is IEnumerable<long> list) return list.ToArray();
        return [Convert.ToInt64(value)];
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

    // ---------- ★ 修复 P0-1：JsonElement 安全解包 ----------

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

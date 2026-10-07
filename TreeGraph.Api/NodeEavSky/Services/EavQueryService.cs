using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.NodeEav;
using TreeGraph.Shared.NodeEav.Dtos;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>动态查询服务（纯 LINQ）：多个过滤器用 Intersect 实现 AND 语义</summary>
public class EavQueryService
{
    private readonly TreeGraphDbContext _db;
    private readonly IAttributeCache _attrCache;
    private readonly ICompositeTypeCache _compositeCache;
    private readonly EavReadService _readService;
    private readonly CompositeValueService _composite;
    private readonly IUnitCache _unitCache;
    private readonly UnitConverter _converter;

    public EavQueryService(
        TreeGraphDbContext db, IAttributeCache attrCache, ICompositeTypeCache compositeCache,
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
            // ★ int + 单位：按 ValueDecimal 排序（归一化后的值）
            EavDataTypes.Int when def.UnitId is not null => (await baseQuery
                .Select(v => new { v.EntityId, v.ValueDecimal })
                .ToListAsync(ct))
                .ToDictionary(x => x.EntityId, x => (object?)x.ValueDecimal),

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
            // ★ int + 单位：走 decimal 过滤路径（数据存于 ValueDecimal）
            EavDataTypes.Int when def.UnitId is not null
                => ApplyDecimalFilter(baseQuery, def, filter),

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

    /// <summary>
    /// 带 entity_id 预过滤的查询（iNode 场景用）。
    ///
    /// ★ 新增，不影响原有 QueryAsync。
    ///
    /// 逻辑：
    ///   1. 走原有 FilterEntityIdsAsync 得到属性过滤后的候选 entity 集合
    ///   2. 与 allowedEntityIds 求交集
    ///   3. 内存分页 + 批量加载
    /// </summary>
    public async Task<PagedResult<DynamicEntity>> QueryWithAllowedIdsAsync(
        string entityType,
        IReadOnlyCollection<string>? allowedEntityIds,
        EavQueryRequest request,
        CancellationToken ct = default)
    {
        // 无限制 → 走原路径
        if (allowedEntityIds is null)
            return await QueryAsync(request, ct);

        if (allowedEntityIds.Count == 0)
        {
            return new PagedResult<DynamicEntity>
            {
                Items = new List<DynamicEntity>(),
                Total = 0,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }

        // 1. 属性过滤得到的 entityIds
        var filtered = await FilterEntityIdsAsync(
            new EavQueryRequest
            {
                EntityType = entityType,
                Filters = request.Filters,
                OrderByAttribute = request.OrderByAttribute,
                OrderDescending = request.OrderDescending,
                // 取全量后再交集分页，所以这里用大 pageSize
                Page = 1,
                PageSize = int.MaxValue
            }, ct);

        // 2. 交集
        var allowed = allowedEntityIds.ToHashSet();
        var intersected = filtered.Items
            .Where(id => allowed.Contains(id))
            .ToList();

        // 3. 分页
        var total = intersected.Count;
        var skip = (request.Page - 1) * request.PageSize;
        var pageIds = intersected.Skip(skip).Take(request.PageSize).ToList();

        if (pageIds.Count == 0)
        {
            return new PagedResult<DynamicEntity>
            {
                Items = new List<DynamicEntity>(),
                Total = total,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }

        // 4. 批量加载实体
        var entities = await _readService.LoadBatchAsync(
            pageIds, entityType, originalUnits: false, ct);

        // 5. 保持 pageIds 的顺序
        var inputOrder = pageIds
            .Select((id, idx) => (id, idx))
            .ToDictionary(x => x.id, x => x.idx);

        var ordered = entities
            .OrderBy(e => inputOrder.GetValueOrDefault(e.EntityId, int.MaxValue))
            .ToList();

        return new PagedResult<DynamicEntity>
        {
            Items = ordered,
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}

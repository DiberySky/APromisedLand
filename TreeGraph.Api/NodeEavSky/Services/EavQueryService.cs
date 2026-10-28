using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.NodeEavSky;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>动态查询服务（纯 LINQ）：多个过滤器用 Intersect 实现 AND 语义</summary>
public class EavQueryService
{
    private readonly TreeGraphDbContext _db;
    private readonly IAttributeCache _attrCache;
    private readonly ICompositeTypeCache _compositeCache;
    private readonly EavReadService _readService;
    private readonly CompositeValueService _composite;

    public EavQueryService(
        TreeGraphDbContext db, IAttributeCache attrCache, ICompositeTypeCache compositeCache,
        EavReadService readService, CompositeValueService composite)
    {
        _db = db;
        _attrCache = attrCache;
        _compositeCache = compositeCache;
        _readService = readService;
        _composite = composite;
    }

    public async Task<PagedResult<string>> FilterEntityIdsAsync(
        EavQueryRequest request, CancellationToken ct = default)
    {
        var definitions = _attrCache.GetDefinitions()
            .ToDictionary(d => d.AttributeName);

        IQueryable<string>? entityQuery = null;

        foreach (var filter in request.Filters)
        {
            if (!definitions.TryGetValue(filter.AttributeName, out var def))
                throw new ArgumentException($"未知属性: {filter.AttributeName}");
            if (!def.IsSearchable)
                throw new ArgumentException($"属性不可搜索: {filter.AttributeName}");

            var subQuery = BuildSingleFilterQuery(def, filter);
            entityQuery = entityQuery is null ? subQuery : entityQuery.Intersect(subQuery);
        }

        entityQuery ??= _db.AttributeValues
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
            // 有排序：LEFT JOIN 排序键行，纯 SQL 排序 + 分页。
            //
            // null 键位置（实体未设排序属性，left join 空侧）：
            //   ASC 排最前、DESC 排最后；同键按 EntityId 升序兜底保证分页稳定。
            //   ——与历史内存排序语义严格一致（见回归测试
            //   Query_OrderByAsc_NullKeysFirst / Query_OrderByDesc_NullKeysLast）。
            //
            // 历史方案是「全量取过滤结果 ID 到内存排序再分页」，规避 EF Core 对
            // Distinct+ORDER BY 相关子查询静默丢序的限制，但数据量大时内存/延迟
            // 不可控；改为 LEFT JOIN 投影排序键后无此限制，排序/分页全部下推 SQL。
            if (!definitions.TryGetValue(request.OrderByAttribute, out var orderDef))
                throw new ArgumentException($"未知排序属性: {request.OrderByAttribute}");
            if (!orderDef.IsSortable)
                throw new ArgumentException($"属性不可排序: {request.OrderByAttribute}");

            var keyRows = _db.AttributeValues
                .Where(v => v.AttributeId == orderDef.AttributeId);

            var sorted = SortByKey(entityQuery, keyRows, orderDef, request.OrderDescending);
            ids = await sorted
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
        }

        return new PagedResult<string>
        {
            Items = ids,
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    /// <summary>
    /// LEFT JOIN 排序键并按方向排序（纯 SQL，调用方再 Skip/Take）。
    /// 每种数据类型显式展开键列投影——EF Core 无法内联委托参数，
    /// 逐类型书写确保表达式可翻译（GroupJoin+SelectMany → LEFT JOIN）。
    /// </summary>
    private static IQueryable<string> SortByKey(
        IQueryable<string> ids, IQueryable<AttributeValue> keyRows,
        AttributeDefinition def, bool descending)
        => def.DataType switch
        {
            // ★ int + 单位：按 ValueDecimal 排序（归一化后的值）
            EavDataTypes.Int when def.UnitId is not null
                => SortDecimal(ids, keyRows, descending),
            EavDataTypes.Int => SortLong(ids, keyRows, descending),
            EavDataTypes.Decimal => SortDecimal(ids, keyRows, descending),
            EavDataTypes.String or EavDataTypes.SingleChoice
                => SortString(ids, keyRows, descending),
            EavDataTypes.Bool => SortBool(ids, keyRows, descending),
            EavDataTypes.Datetime => SortDatetime(ids, keyRows, descending),
            EavDataTypes.Date => SortDate(ids, keyRows, descending),
            EavDataTypes.Time => SortTime(ids, keyRows, descending),
            _ => throw new NotSupportedException(
                $"不支持按 {def.DataType} 类型排序")
        };

    private static IQueryable<string> SortLong(
        IQueryable<string> ids, IQueryable<AttributeValue> keys, bool desc)
    {
        var joined = ids
            .GroupJoin(keys, id => id, v => v.EntityId, (id, vs) => new { id, vs })
            .SelectMany(x => x.vs.DefaultIfEmpty(),
                (x, v) => new { x.id, Key = (long?)v!.ValueInt });
        return desc
            ? joined.OrderBy(x => x.Key == null ? 1 : 0)
                .ThenByDescending(x => x.Key).ThenBy(x => x.id).Select(x => x.id)
            : joined.OrderBy(x => x.Key == null ? 0 : 1)
                .ThenBy(x => x.Key).ThenBy(x => x.id).Select(x => x.id);
    }

    private static IQueryable<string> SortDecimal(
        IQueryable<string> ids, IQueryable<AttributeValue> keys, bool desc)
    {
        var joined = ids
            .GroupJoin(keys, id => id, v => v.EntityId, (id, vs) => new { id, vs })
            .SelectMany(x => x.vs.DefaultIfEmpty(),
                (x, v) => new { x.id, Key = (decimal?)v!.ValueDecimal });
        return desc
            ? joined.OrderBy(x => x.Key == null ? 1 : 0)
                .ThenByDescending(x => x.Key).ThenBy(x => x.id).Select(x => x.id)
            : joined.OrderBy(x => x.Key == null ? 0 : 1)
                .ThenBy(x => x.Key).ThenBy(x => x.id).Select(x => x.id);
    }

    private static IQueryable<string> SortString(
        IQueryable<string> ids, IQueryable<AttributeValue> keys, bool desc)
    {
        var joined = ids
            .GroupJoin(keys, id => id, v => v.EntityId, (id, vs) => new { id, vs })
            .SelectMany(x => x.vs.DefaultIfEmpty(),
                (x, v) => new { x.id, Key = v!.ValueString });
        return desc
            ? joined.OrderBy(x => x.Key == null ? 1 : 0)
                .ThenByDescending(x => x.Key).ThenBy(x => x.id).Select(x => x.id)
            : joined.OrderBy(x => x.Key == null ? 0 : 1)
                .ThenBy(x => x.Key).ThenBy(x => x.id).Select(x => x.id);
    }

    private static IQueryable<string> SortBool(
        IQueryable<string> ids, IQueryable<AttributeValue> keys, bool desc)
    {
        var joined = ids
            .GroupJoin(keys, id => id, v => v.EntityId, (id, vs) => new { id, vs })
            .SelectMany(x => x.vs.DefaultIfEmpty(),
                (x, v) => new { x.id, Key = (bool?)v!.ValueBool });
        return desc
            ? joined.OrderBy(x => x.Key == null ? 1 : 0)
                .ThenByDescending(x => x.Key).ThenBy(x => x.id).Select(x => x.id)
            : joined.OrderBy(x => x.Key == null ? 0 : 1)
                .ThenBy(x => x.Key).ThenBy(x => x.id).Select(x => x.id);
    }

    private static IQueryable<string> SortDatetime(
        IQueryable<string> ids, IQueryable<AttributeValue> keys, bool desc)
    {
        var joined = ids
            .GroupJoin(keys, id => id, v => v.EntityId, (id, vs) => new { id, vs })
            .SelectMany(x => x.vs.DefaultIfEmpty(),
                (x, v) => new { x.id, Key = (DateTimeOffset?)v!.ValueDatetime });
        return desc
            ? joined.OrderBy(x => x.Key == null ? 1 : 0)
                .ThenByDescending(x => x.Key).ThenBy(x => x.id).Select(x => x.id)
            : joined.OrderBy(x => x.Key == null ? 0 : 1)
                .ThenBy(x => x.Key).ThenBy(x => x.id).Select(x => x.id);
    }

    private static IQueryable<string> SortDate(
        IQueryable<string> ids, IQueryable<AttributeValue> keys, bool desc)
    {
        var joined = ids
            .GroupJoin(keys, id => id, v => v.EntityId, (id, vs) => new { id, vs })
            .SelectMany(x => x.vs.DefaultIfEmpty(),
                (x, v) => new { x.id, Key = (DateOnly?)v!.ValueDateOnly });
        return desc
            ? joined.OrderBy(x => x.Key == null ? 1 : 0)
                .ThenByDescending(x => x.Key).ThenBy(x => x.id).Select(x => x.id)
            : joined.OrderBy(x => x.Key == null ? 0 : 1)
                .ThenBy(x => x.Key).ThenBy(x => x.id).Select(x => x.id);
    }

    private static IQueryable<string> SortTime(
        IQueryable<string> ids, IQueryable<AttributeValue> keys, bool desc)
    {
        var joined = ids
            .GroupJoin(keys, id => id, v => v.EntityId, (id, vs) => new { id, vs })
            .SelectMany(x => x.vs.DefaultIfEmpty(),
                (x, v) => new { x.id, Key = (TimeOnly?)v!.ValueTime });
        return desc
            ? joined.OrderBy(x => x.Key == null ? 1 : 0)
                .ThenByDescending(x => x.Key).ThenBy(x => x.id).Select(x => x.id)
            : joined.OrderBy(x => x.Key == null ? 0 : 1)
                .ThenBy(x => x.Key).ThenBy(x => x.id).Select(x => x.id);
    }

    public async Task<PagedResult<DynamicEntity>> QueryAsync(
        EavQueryRequest request, CancellationToken ct = default)
    {
        var idResult = await FilterEntityIdsAsync(request, ct);
        var entities = await _readService.LoadBatchAsync(idResult.Items, ct);

        return new PagedResult<DynamicEntity>
        {
            Items = entities,
            Total = idResult.Total,
            Page = idResult.Page,
            PageSize = idResult.PageSize
        };
    }

    private IQueryable<string> BuildSingleFilterQuery(
        AttributeDefinition def, AttributeFilter filter)
    {
        if (def.DataType == EavDataTypes.Composite)
            return BuildCompositeFilterQuery(def, filter);

        if (def.DataType == EavDataTypes.Json)
            return BuildJsonFilterQuery(def, filter);

        var baseQuery = _db.AttributeValues
            .Where(v => v.AttributeId == def.AttributeId);

        if (filter.Operator == "unit_eq")
        {
            var unitId = filter.Value!.ToString()!;
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

    // ============================================================
    // json 类型查询（自由 JSON 对象，存 value_jsonb）
    // ============================================================

    /// <summary>
    /// json 类型属性查询。
    ///   - 无 FieldPath：按整个 JSON 原文做字符串匹配（eq / like / startswith / endswith）
    ///   - 有 FieldPath：沿路径取值后，根据目标节点 jsonb_typeof 动态选择比较方式
    ///     （number → 数值比较，string → 字符串比较，bool → 布尔比较）
    ///
    /// 路径语法：用 "." 分隔，如 "address.city"；最多支持 2 层
    /// </summary>
    private IQueryable<string> BuildJsonFilterQuery(
        AttributeDefinition def, AttributeFilter filter)
    {
        var baseQuery = _db.AttributeValues
            .Where(v => v.AttributeId == def.AttributeId
                     && v.ValueJsonb != null);

        // 无 FieldPath → 整体匹配
        if (string.IsNullOrEmpty(filter.FieldPath))
        {
            var filtered = filter.Operator switch
            {
                "eq" => baseQuery.Where(v =>
                    v.ValueJsonb!.RootElement.GetRawText() == filter.Value!.ToString()),
                "like" => baseQuery.Where(v =>
                    v.ValueJsonb!.RootElement.GetRawText().Contains(filter.Value!.ToString()!)),
                "startswith" => baseQuery.Where(v =>
                    v.ValueJsonb!.RootElement.GetRawText().StartsWith(filter.Value!.ToString()!)),
                "endswith" => baseQuery.Where(v =>
                    v.ValueJsonb!.RootElement.GetRawText().EndsWith(filter.Value!.ToString()!)),
                _ => throw new NotSupportedException(
                    $"json 整体匹配不支持运算符: {filter.Operator}（可尝试加 FieldPath 做路径查询）")
            };
            return filtered.Select(v => v.EntityId).Distinct();
        }

        // 有 FieldPath → 路径查询
        var pathSegments = filter.FieldPath.Split('.');
        if (pathSegments.Length > 3)
            throw new NotSupportedException("json 路径查询最多支持 3 层嵌套");

        var query = pathSegments.Length switch
        {
            1 => ApplyJsonPathFilterSingle(baseQuery, pathSegments[0], filter),
            2 => ApplyJsonPathFilterNested(baseQuery, pathSegments[0], pathSegments[1], filter),
            3 => ApplyJsonPathFilterTriple(baseQuery,
                pathSegments[0], pathSegments[1], pathSegments[2], filter),
            _ => throw new InvalidOperationException()
        };

        return query.Select(v => v.EntityId).Distinct();
    }

    /// <summary>单层 json 路径：jsonb 顶层 key</summary>
    private static IQueryable<AttributeValue> ApplyJsonPathFilterSingle(
        IQueryable<AttributeValue> query, string key, AttributeFilter filter)
    {
        // number 类型 → 数值比较
        if (filter.Value is decimal or int or long or double or float)
        {
            var target = Convert.ToDecimal(filter.Value, CultureInfo.InvariantCulture);
            return filter.Operator switch
            {
                "eq" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(key)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(key).GetDecimal() == target),
                "neq" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(key)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(key).GetDecimal() != target),
                "gt" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(key)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(key).GetDecimal() > target),
                "gte" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(key)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(key).GetDecimal() >= target),
                "lt" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(key)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(key).GetDecimal() < target),
                "lte" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(key)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(key).GetDecimal() <= target),
                _ => throw new NotSupportedException($"不支持的运算符: {filter.Operator}")
            };
        }

        // bool 类型
        if (filter.Value is bool bv)
        {
            return filter.Operator == "eq"
                ? query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(key)) == "boolean"
                    && v.ValueJsonb!.RootElement.GetProperty(key).GetBoolean() == bv)
                : throw new NotSupportedException($"bool 字段仅支持 eq，不支持: {filter.Operator}");
        }

        // 默认 string 类型（也支持 like / startswith / endswith）
        var sv = filter.Value!.ToString();
        return filter.Operator switch
        {
            "eq" => query.Where(v =>
                EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(key)) == "string"
                && v.ValueJsonb!.RootElement.GetProperty(key).GetString() == sv),
            "neq" => query.Where(v =>
                EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(key)) == "string"
                && v.ValueJsonb!.RootElement.GetProperty(key).GetString() != sv),
            "like" => query.Where(v =>
                EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(key)) == "string"
                && v.ValueJsonb!.RootElement.GetProperty(key).GetString()!.Contains(sv!)),
            "startswith" => query.Where(v =>
                EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(key)) == "string"
                && v.ValueJsonb!.RootElement.GetProperty(key).GetString()!.StartsWith(sv!)),
            "endswith" => query.Where(v =>
                EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(key)) == "string"
                && v.ValueJsonb!.RootElement.GetProperty(key).GetString()!.EndsWith(sv!)),
            _ => throw new NotSupportedException($"不支持的运算符: {filter.Operator}")
        };
    }

    /// <summary>两层嵌套 json 路径</summary>
    private static IQueryable<AttributeValue> ApplyJsonPathFilterNested(
        IQueryable<AttributeValue> query, string outer, string inner, AttributeFilter filter)
    {
        if (filter.Value is decimal or int or long or double or float)
        {
            var target = Convert.ToDecimal(filter.Value, CultureInfo.InvariantCulture);
            return filter.Operator switch
            {
                "eq" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner).GetDecimal() == target),
                "neq" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner).GetDecimal() != target),
                "gt" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner).GetDecimal() > target),
                "gte" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner).GetDecimal() >= target),
                "lt" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner).GetDecimal() < target),
                "lte" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner).GetDecimal() <= target),
                _ => throw new NotSupportedException($"不支持的运算符: {filter.Operator}")
            };
        }

        if (filter.Value is bool bv)
        {
            return filter.Operator == "eq"
                ? query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner)) == "boolean"
                    && v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner).GetBoolean() == bv)
                : throw new NotSupportedException($"bool 字段仅支持 eq，不支持: {filter.Operator}");
        }

        var sv = filter.Value!.ToString();
        return filter.Operator switch
        {
            "eq" => query.Where(v =>
                EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner)) == "string"
                && v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner).GetString() == sv),
            "neq" => query.Where(v =>
                EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner)) == "string"
                && v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner).GetString() != sv),
            "like" => query.Where(v =>
                EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner)) == "string"
                && v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner).GetString()!.Contains(sv!)),
            "startswith" => query.Where(v =>
                EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner)) == "string"
                && v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner).GetString()!.StartsWith(sv!)),
            "endswith" => query.Where(v =>
                EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner)) == "string"
                && v.ValueJsonb!.RootElement.GetProperty(outer).GetProperty(inner).GetString()!.EndsWith(sv!)),
            _ => throw new NotSupportedException($"不支持的运算符: {filter.Operator}")
        };
    }

    /// <summary>三层嵌套 json 路径</summary>
    private static IQueryable<AttributeValue> ApplyJsonPathFilterTriple(
        IQueryable<AttributeValue> query, string p1, string p2, string p3, AttributeFilter filter)
    {
        if (filter.Value is decimal or int or long or double or float)
        {
            var target = Convert.ToDecimal(filter.Value, CultureInfo.InvariantCulture);
            return filter.Operator switch
            {
                "eq" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3).GetDecimal() == target),
                "gt" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3).GetDecimal() > target),
                "gte" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3).GetDecimal() >= target),
                "lt" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3).GetDecimal() < target),
                "lte" => query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3)) == "number"
                    && v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3).GetDecimal() <= target),
                _ => throw new NotSupportedException($"不支持的运算符: {filter.Operator}")
            };
        }

        if (filter.Value is bool bv)
        {
            return filter.Operator == "eq"
                ? query.Where(v =>
                    EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3)) == "boolean"
                    && v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3).GetBoolean() == bv)
                : throw new NotSupportedException($"bool 字段仅支持 eq，不支持: {filter.Operator}");
        }

        var sv = filter.Value!.ToString();
        return filter.Operator switch
        {
            "eq" => query.Where(v =>
                EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3)) == "string"
                && v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3).GetString() == sv),
            "neq" => query.Where(v =>
                EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3)) == "string"
                && v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3).GetString() != sv),
            "like" => query.Where(v =>
                EF.Functions.JsonTypeof(v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3)) == "string"
                && v.ValueJsonb!.RootElement.GetProperty(p1).GetProperty(p2).GetProperty(p3).GetString()!.Contains(sv!)),
            _ => throw new NotSupportedException($"不支持的运算符: {filter.Operator}")
        };
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

        var (rawValue, _) = ParseNumericFilterValue(f.Value);
        if (rawValue is null) throw new ArgumentException("过滤值格式错误");
        var v = rawValue.Value;
        decimal? v2 = f.Value2 is null ? null : ParseNumericFilterValue2(f.Value2);

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

        var (rawValue, _) = ParseNumericFilterValue(f.Value);
        if (rawValue is null) throw new ArgumentException("过滤值格式错误");
        var v = rawValue.Value;
        decimal? v2 = f.Value2 is null ? null : ParseNumericFilterValue2(f.Value2);

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


    private static decimal ParseNumericFilterValue2(object? value2)
    {
        var (rawValue, _) = ParseNumericFilterValue(value2);
        if (rawValue is null) throw new ArgumentException("过滤值格式错误");
        return rawValue.Value;
    }

    private static (decimal? Value, string? UnitId) ParseNumericFilterValue(object? value)
    {
        return value switch
        {
            null => (null, null),
            NumericValue nv => (nv.Value, nv.UnitId),
            JsonElement je when je.ValueKind == JsonValueKind.Object =>
                (je.GetProperty("value").GetDecimal(),
                 je.TryGetProperty("unitId", out var uid) && uid.ValueKind == JsonValueKind.String
                     ? uid.GetString() : null),
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
        AttributeDefinition def, AttributeFilter filter)
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
            .Where(v => v.AttributeId == def.AttributeId);

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
        var entities = await _readService.LoadBatchAsync(pageIds, ct);

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

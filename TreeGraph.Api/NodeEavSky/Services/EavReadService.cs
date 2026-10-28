using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.NodeEavSky;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>读取服务：按元数据把类型化值列还原为运行时值</summary>
public class EavReadService
{
    private readonly TreeGraphDbContext _db;
    private readonly IAttributeCache _attrCache;
    private readonly CompositeValueService _composite;
    private readonly IOptionSetCache _optionSetCache;

    public EavReadService(
        TreeGraphDbContext db, IAttributeCache attrCache, CompositeValueService composite,
        IOptionSetCache optionSetCache)
    {
        _db = db;
        _attrCache = attrCache;
        _composite = composite;
        _optionSetCache = optionSetCache;
    }

    public async Task<DynamicEntity> LoadAsync(
        string entityId,
        CancellationToken ct = default)
    {
        var definitions = _attrCache.GetDefinitions();
        var defMap = definitions.ToDictionary(d => d.AttributeId);

        var rows = await _db.AttributeValues
            .Where(v => v.EntityId == entityId)
            .AsNoTracking()
            .ToListAsync(ct);

        var locRows = await _db.LocationAttributeValues
            .Where(l => l.EntityId == entityId)
            .AsNoTracking()
            .ToListAsync(ct);

        var result = new DynamicEntity(entityId);

        // ★ #3：填充 UpdatedAt（供乐观锁使用），需合并类型化值表与位置表
        var valuesMax = rows.Count > 0 ? rows.Max(r => r.UpdatedAt) : (DateTimeOffset?)null;
        var locMax = locRows.Count > 0 ? locRows.Max(r => r.UpdatedAt) : (DateTimeOffset?)null;
        DateTimeOffset? overallMax = null;
        foreach (var m in new[] { valuesMax, locMax })
        {
            if (m is { } v && (overallMax is null || v > overallMax)) overallMax = v;
        }
        result.UpdatedAt = overallMax;

        foreach (var row in rows)
        {
            if (!defMap.TryGetValue(row.AttributeId, out var def)) continue;
            var value = ExtractTypedValue(row, def);
            result.SetProperty(def.AttributeName, value);
        }

        foreach (var loc in locRows)
        {
            if (defMap.TryGetValue(loc.AttributeId, out var def)
                && def.DataType == EavDataTypes.Location)
            {
                result.SetProperty(def.AttributeName, ToLocationValue(loc));
            }
        }

        return result;
    }

    public async Task<List<DynamicEntity>> LoadBatchAsync(
        IEnumerable<string> entityIds,
        CancellationToken ct = default)
    {
        var ids = entityIds.ToList();
        var definitions = _attrCache.GetDefinitions();
        var defMap = definitions.ToDictionary(d => d.AttributeId);

        var allRows = await _db.AttributeValues
            .Where(v => ids.Contains(v.EntityId))
            .AsNoTracking()
            .ToListAsync(ct);

        var allLocRows = await _db.LocationAttributeValues
            .Where(l => ids.Contains(l.EntityId))
            .AsNoTracking()
            .ToListAsync(ct);

        var grouped = allRows.GroupBy(r => r.EntityId);
        var locGrouped = allLocRows.GroupBy(l => l.EntityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // ★ 保持输入 ids 的顺序——FilterEntityIdsAsync 已按业务键排好序，
        //   GroupBy 的输出顺序由 DB 行序决定，必须显式按输入序重排。
        var inputOrder = ids
            .Select((id, idx) => (id, idx))
            .ToDictionary(x => x.id, x => x.idx);

        // 普通值表与位置表出现过的实体并集
        var allEntityIds = grouped.Select(g => g.Key)
            .Union(locGrouped.Keys)
            .ToList();

        return allEntityIds.Select(entityId =>
        {
            var entity = new DynamicEntity(entityId);

            DateTimeOffset? maxUpdated = null;

            if (grouped.FirstOrDefault(g => g.Key == entityId) is { } gv)
            {
                maxUpdated = gv.Max(r => r.UpdatedAt);
                foreach (var row in gv)
                {
                    if (defMap.TryGetValue(row.AttributeId, out var def))
                        entity.SetProperty(def.AttributeName, ExtractTypedValue(row, def));
                }
            }

            if (locGrouped.TryGetValue(entityId, out var locs))
            {
                var locMax = locs.Max(l => l.UpdatedAt);
                maxUpdated = maxUpdated is null || locMax > maxUpdated ? locMax : maxUpdated;
                foreach (var loc in locs)
                {
                    if (defMap.TryGetValue(loc.AttributeId, out var def)
                        && def.DataType == EavDataTypes.Location)
                    {
                        entity.SetProperty(def.AttributeName, ToLocationValue(loc));
                    }
                }
            }

            entity.UpdatedAt = maxUpdated;
            return entity;
        })
        .OrderBy(e => inputOrder.GetValueOrDefault(e.EntityId, int.MaxValue))
        .ToList();
    }

    private static EavLocationValue ToLocationValue(LocationAttributeValue l) => new()
    {
        Latitude = l.Latitude,
        Longitude = l.Longitude,
        Label = l.Label,
        Address = l.Address,
        CoordinateSystem = l.CoordinateSystem
    };

    private object? ExtractTypedValue(AttributeValue row, AttributeDefinition def)
    {
        return def.DataType switch
        {
            EavDataTypes.String => row.ValueString,
            EavDataTypes.SingleChoice => ExtractSingleChoice(row, def),

            // ★ int + 单位：读 ValueDecimal
            EavDataTypes.Int when def.UnitId is not null => ExtractNumericValue(
                row.ValueDecimal, row.UnitId, def),

            // int 无单位：读 ValueInt
            EavDataTypes.Int => ExtractNumericValue(
                row.ValueInt is { } i ? i : null, row.UnitId, def),

            EavDataTypes.Decimal => ExtractNumericValue(
                row.ValueDecimal, row.UnitId, def),
            EavDataTypes.Bool => row.ValueBool,
            EavDataTypes.Datetime => row.ValueDatetime,
            EavDataTypes.Date => row.ValueDateOnly,
            EavDataTypes.Time => row.ValueTime,
            EavDataTypes.File => row.ValueFileMeta,
            EavDataTypes.Json => row.ValueJsonb,
            EavDataTypes.Composite => row.ValueJsonb is null
                ? null
                : _composite.Deserialize(row.ValueJsonb, def.RefCompositeTypeId!),
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
    /// ★ 当属性未绑定单位时，int 类型必须返回 long（而非 decimal），
    /// 否则前端会收到 decimal 的 JSON 数字（含小数位）与 API 契约不符。
    /// 绑定单位时返回 NumericValue（单位仅作记录，取输入时记录的单位）。
    /// </summary>
    private object? ExtractNumericValue(
        decimal? value, string? recordUnitId, AttributeDefinition def)
    {
        if (value is null) return null;

        if (def.UnitId is not { } unitId)
        {
            // 无单位绑定：按数据类型返回对应 C# 类型
            return def.DataType == EavDataTypes.Int
                ? (object)(long)value.Value
                : value.Value;
        }

        return new NumericValue(value.Value, recordUnitId ?? unitId);
    }

    public async Task<List<AttributeAuditLog>> GetHistoryAsync(
        string entityId, DateTimeOffset? from = null,
        CancellationToken ct = default)
    {
        var query = _db.AttributeAuditLogs
            .Where(a => a.EntityId == entityId);

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

    /// <summary>
    /// ★ #3：实体所有 AttributeValue 行的最大 UpdatedAt。
    /// 无属性值时（新建）为 null。
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    public DynamicEntity(string id) { EntityId = id; }

    public void SetProperty(string name, object? value) => _props[name] = value;
    public object? GetProperty(string name) => _props.TryGetValue(name, out var v) ? v : null;
    public IReadOnlyDictionary<string, object?> Properties => _props;
}

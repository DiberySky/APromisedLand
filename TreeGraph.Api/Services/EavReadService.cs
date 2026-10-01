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

    public async Task<DynamicEntity> LoadAsync(
        long entityId, string entityType, bool originalUnits = false,
        CancellationToken ct = default)
    {
        var definitions = _attrCache.GetDefinitions(entityType);
        var defMap = definitions.ToDictionary(d => d.AttributeId);

        var rows = await _db.AttributeValues
            .Where(v => v.EntityType == entityType && v.EntityId == entityId)
            .AsNoTracking()
            .ToListAsync(ct);

        var result = new DynamicEntity(entityId, entityType);

        foreach (var row in rows)
        {
            if (!defMap.TryGetValue(row.AttributeId, out var def)) continue;
            var value = ExtractTypedValue(row, def, originalUnits);
            result.SetProperty(def.AttributeName, value);
        }

        return result;
    }

    public async Task<List<DynamicEntity>> LoadBatchAsync(
        IEnumerable<long> entityIds, string entityType,
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

        return grouped.Select(g =>
        {
            var entity = new DynamicEntity(g.Key, entityType);
            foreach (var row in g)
            {
                if (defMap.TryGetValue(row.AttributeId, out var def))
                {
                    entity.SetProperty(def.AttributeName,
                        ExtractTypedValue(row, def, originalUnits));
                }
            }
            return entity;
        }).ToList();
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
                : _composite.Deserialize(row.ValueJsonb, def.RefCompositeTypeId!.Value),
            _ => null
        };
    }

    /// <summary>
    /// 单选值提取：返回 { value, label }。
    /// ★ 修复：选项集已被删除时降级为裸 Value，避免 NRE。
    /// </summary>
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
            // 选项集已被删除，降级为裸 Value
            return row.ValueString;
        }
    }

    private object? ExtractNumericValue(
        decimal? baseValue, Guid? originalUnitId, AttributeDefinition def, bool originalUnits)
    {
        if (baseValue is null) return null;

        if (def.UnitId is not { } baseUnitId)
            return baseValue.Value;

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
            // 原始单位已被删除：回退到基准单位
            return new NumericValue(baseValue.Value, baseUnitId);
        }
    }

    public async Task<List<AttributeAuditLog>> GetHistoryAsync(
        long entityId, string entityType, DateTimeOffset? from = null,
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
    public long EntityId { get; }
    public string EntityType { get; }

    public DynamicEntity(long id, string type) { EntityId = id; EntityType = type; }

    public void SetProperty(string name, object? value) => _props[name] = value;
    public object? GetProperty(string name) => _props.TryGetValue(name, out var v) ? v : null;
    public IReadOnlyDictionary<string, object?> Properties => _props;
}

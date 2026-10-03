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

            // ★ int + 单位：读 ValueDecimal（归一化后的值）
            EavDataTypes.Int when def.UnitId is not null => ExtractNumericValue(
                row.ValueDecimal, row.UnitId, def, originalUnits),

            // int 无单位：读 ValueInt
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

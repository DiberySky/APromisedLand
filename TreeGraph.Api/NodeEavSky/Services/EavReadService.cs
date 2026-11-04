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
        string entityId, string entityType,
        CancellationToken ct = default)
    {
        var definitions = _attrCache.GetDefinitions(entityType);
        var defMap = definitions.ToDictionary(d => d.AttributeId);

        var rows = await _db.AttributeValues
            .Where(v => v.EntityId == entityId)
            .AsNoTracking()
            .ToListAsync(ct);

        var locRows = await _db.LocationAttributeValues
            .Where(l => l.EntityId == entityId)
            .AsNoTracking()
            .ToListAsync(ct);

        // property_tree：挂载关系走 eav_tree_roots 映射表（值 = { rootId, name }）
        var treeRoots = await _db.EavTreeRoots
            .Where(x => x.EntityId == entityId)
            .AsNoTracking()
            .ToListAsync(ct);

        var rootNames = await LoadRootNamesAsync(treeRoots.Select(x => x.Id), ct);

        var result = new DynamicEntity(entityId, entityType);

        // ★ #3：填充 UpdatedAt（供乐观锁使用），需合并类型化值表、位置表与属性树映射表
        var valuesMax = rows.Count > 0 ? rows.Max(r => r.UpdatedAt) : (DateTimeOffset?)null;
        var locMax = locRows.Count > 0 ? locRows.Max(r => r.UpdatedAt) : (DateTimeOffset?)null;
        var treeRootMax = treeRoots.Count > 0 ? treeRoots.Max(x => x.CreatedAt) : (DateTimeOffset?)null;
        DateTimeOffset? overallMax = null;
        foreach (var m in new[] { valuesMax, locMax, treeRootMax })
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

        foreach (var tr in treeRoots)
        {
            if (defMap.TryGetValue(tr.AttributeId, out var def)
                && def.DataType == EavDataTypes.PropertyTree)
            {
                result.SetProperty(def.AttributeName,
                    new PropertyTreeValue(tr.Id, rootNames.GetValueOrDefault(tr.Id)));
            }
        }

        return result;
    }

    public async Task<List<DynamicEntity>> LoadBatchAsync(
        IEnumerable<string> entityIds, string entityType,
        CancellationToken ct = default)
    {
        var ids = entityIds.ToList();
        var definitions = _attrCache.GetDefinitions(entityType);
        var defMap = definitions.ToDictionary(d => d.AttributeId);

        var allRows = await _db.AttributeValues
            .Where(v => ids.Contains(v.EntityId))
            .AsNoTracking()
            .ToListAsync(ct);

        var allLocRows = await _db.LocationAttributeValues
            .Where(l => ids.Contains(l.EntityId))
            .AsNoTracking()
            .ToListAsync(ct);

        // property_tree：挂载关系走 eav_tree_roots 映射表
        var allTreeRoots = await _db.EavTreeRoots
            .Where(x => ids.Contains(x.EntityId))
            .AsNoTracking()
            .ToListAsync(ct);

        var rootNames = await LoadRootNamesAsync(allTreeRoots.Select(x => x.Id), ct);

        var grouped = allRows.GroupBy(r => r.EntityId);
        var locGrouped = allLocRows.GroupBy(l => l.EntityId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var treeRootGrouped = allTreeRoots.GroupBy(x => x.EntityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // ★ 保持输入 ids 的顺序——FilterEntityIdsAsync 已按业务键排好序，
        //   GroupBy 的输出顺序由 DB 行序决定，必须显式按输入序重排。
        var inputOrder = ids
            .Select((id, idx) => (id, idx))
            .ToDictionary(x => x.id, x => x.idx);

        // 普通值表、位置表与属性树映射表出现过的实体并集
        var allEntityIds = grouped.Select(g => g.Key)
            .Union(locGrouped.Keys)
            .Union(treeRootGrouped.Keys)
            .ToList();

        return allEntityIds.Select(entityId =>
        {
            var entity = new DynamicEntity(entityId, entityType);

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

            if (treeRootGrouped.TryGetValue(entityId, out var roots))
            {
                var rootMax = roots.Max(x => x.CreatedAt);
                maxUpdated = maxUpdated is null || rootMax > maxUpdated ? rootMax : maxUpdated;
                foreach (var tr in roots)
                {
                    if (defMap.TryGetValue(tr.AttributeId, out var def)
                        && def.DataType == EavDataTypes.PropertyTree)
                    {
                        entity.SetProperty(def.AttributeName,
                            new PropertyTreeValue(tr.Id, rootNames.GetValueOrDefault(tr.Id)));
                    }
                }
            }

            entity.UpdatedAt = maxUpdated;
            return entity;
        })
        .OrderBy(e => inputOrder.GetValueOrDefault(e.EntityId, int.MaxValue))
        .ToList();
    }

    /// <summary>批量取属性树根节点名（rootNode.Id → nodes.name），供 { rootId, name } 值组装。</summary>
    private async Task<Dictionary<string, string?>> LoadRootNamesAsync(
        IEnumerable<string> rootIds, CancellationToken ct)
    {
        var idList = rootIds.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<string, string?>();

        return await _db.Nodes
            .Where(n => idList.Contains(n.Id))
            .AsNoTracking()
            .ToDictionaryAsync(n => n.Id, n => (string?)n.Name, ct);
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

            // ★ 方案A：int 恒读 ValueInt 返回 long（避免 JSON 小数位与契约不符）；
            //   decimal 恒读 ValueDecimal。
            EavDataTypes.Int => row.ValueInt,
            EavDataTypes.Decimal => row.ValueDecimal,
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

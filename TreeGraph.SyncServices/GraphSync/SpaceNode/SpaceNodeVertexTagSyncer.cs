using System.Text.Json;
using TreeGraph.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using TreeGraph.GraphSync;
using TreeGraph.GraphSync.SpaceNode;

namespace TreeGraph.SyncServices.GraphSync.SpaceNode;

/// <summary>
/// 空间节点顶点 → 派生 Tag 值同步（S4，消费 GraphSynchronizer 既有 InodeChangedEvent 管道，无独立消费者）。
///
/// 期望集计算（PG 唯一事实源）：
///   节点的全部绑定（inode_entities，DisplayOrder→BindingId 升序）；
///   每个 EntityType 只取主绑定（第一排序键 DisplayOrder，第二 BindingId）；
///   主绑定 EntityType 下的每个 composite 属性，若其来源在本空间有活动账本行（tag_projections），
///   则该 Tag 进入期望集；属性值 = attribute_values 主绑定 EntityId 行的 value_jsonb。
///
/// 收敛（P0-4）：
///   写值 —— 直接构造 INSERT VERTEX nGQL（REST vertices 端点无法表达 date/time/timestamp 字面量）；
///   删除 —— FETCH PROP ON * 读实际 Tag 集 → 再回查 PG 期望集 →
///           仅对「实际 - 期望」∩ 受管集合（本空间活动账本 Tag 名）执行 DELETE TAG。
///   系统 Tag（space_node）与未知 Tag 绝不触碰。
/// </summary>
public sealed class SpaceNodeVertexTagSyncer
{
    private readonly TreeGraphDbContext _db;
    private readonly NebulaGraphClient _client;
    private readonly ILogger<SpaceNodeVertexTagSyncer> _logger;

    public SpaceNodeVertexTagSyncer(
        TreeGraphDbContext db, NebulaGraphClient client, ILogger<SpaceNodeVertexTagSyncer> logger)
    {
        _db = db;
        _client = client;
        _logger = logger;
    }

    /// <summary>同步一个 SpaceNode 顶点的派生 Tag（写值 + 条件收敛多余受管 Tag）。</summary>
    public async Task SyncAsync(string space, string pgSpaceId, string inodeId, CancellationToken ct)
    {
        var expected = await ComputeExpectedAsync(pgSpaceId, inodeId, ct);

        // 1) 写期望集（INSERT 覆盖语义，幂等；多 Tag 顶点互不影响，S0-2 已验证）
        foreach (var (tagName, ngql) in expected.Select(x => (x.Key, BuildInsertNgql(inodeId, x.Key, x.Value))))
            await _client.QueryAsync(space, ngql, ct);

        // 2) 收敛多余受管 Tag
        var actual = await FetchVertexTagsAsync(space, inodeId, ct);
        var managed = await ManagedTagNamesAsync(pgSpaceId, ct);
        var toDelete = actual.Intersect(managed, StringComparer.Ordinal)
            .Except(expected.Keys, StringComparer.Ordinal)
            .ToList();
        if (toDelete.Count == 0) return;

        // P0-4：DELETE 前再次回查 PG 期望集，防「FETCH 读到旧态后新值已写入」的并发误删
        var recheck = await ComputeExpectedAsync(pgSpaceId, inodeId, ct);
        foreach (var tag in toDelete.Except(recheck.Keys, StringComparer.Ordinal))
        {
            // 3) 删前确认 PG 当前仍不期望（含账本活动行存在）
            await _client.QueryAsync(space, $"DELETE TAG `{tag}` FROM \"{inodeId}\"", ct);
            _logger.LogInformation("派生 Tag 已卸载：{Space}.{Tag} FROM {Vid}", space, tag, inodeId);
        }
    }

    /// <summary>受管集合 = 本空间活动账本 Tag 名。</summary>
    private async Task<HashSet<string>> ManagedTagNamesAsync(string pgSpaceId, CancellationToken ct)
        => (await _db.TagProjections.AsNoTracking()
                .Where(p => p.SpaceId == pgSpaceId && !p.IsDeleted)
                .Select(p => p.TagName)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>期望集：Tag 名 → 列值字典（含类型标记，供 nGQL 字面量构造）。</summary>
    private async Task<Dictionary<string, Dictionary<string, object?>>> ComputeExpectedAsync(
        string pgSpaceId, string inodeId, CancellationToken ct)
    {
        // 主绑定：同 EntityType 按 DisplayOrder→BindingId 取首
        var bindings = await _db.InodeEntities.AsNoTracking()
            .Where(b => b.InodeId == inodeId)
            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.BindingId)
            .ToListAsync(ct);
        var primaryByType = bindings
            .GroupBy(b => b.EntityType, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        if (primaryByType.Count == 0) return new(StringComparer.Ordinal);

        // 本空间活动账本（按来源属性索引）
        var ledgerRows = await _db.TagProjections.AsNoTracking()
            .Where(p => p.SpaceId == pgSpaceId && !p.IsDeleted)
            .ToListAsync(ct);
        if (ledgerRows.Count == 0) return new(StringComparer.Ordinal);

        var sourceAttrIds = ledgerRows.Select(r => r.SourceAttributeId).Distinct().ToList();
        var attrs = await _db.AttributeCatalog.AsNoTracking()
            .Where(a => sourceAttrIds.Contains(a.AttributeId))
            .ToListAsync(ct);

        var entityIds = primaryByType.Values.Select(b => b.EntityId).ToList();
        var values = await _db.AttributeValues.AsNoTracking()
            .Where(v => entityIds.Contains(v.EntityId) && sourceAttrIds.Contains(v.AttributeId))
            .Select(v => new { v.EntityId, v.AttributeId, v.ValueJsonb })
            .ToListAsync(ct);
        var valueByAttr = values.ToDictionary(x => x.AttributeId, x => x.ValueJsonb, StringComparer.Ordinal);

        var expected = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var row in ledgerRows)
        {
            var attr = attrs.FirstOrDefault(a => a.AttributeId == row.SourceAttributeId);
            if (attr is null) continue;
            if (!primaryByType.TryGetValue(attr.EntityType, out var binding)) continue;

            var schema = TagProjectionSchema.FromJson(row.SchemaSnapshot);
            var props = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (valueByAttr.TryGetValue(attr.AttributeId, out var json) && json is not null)
                props = MapJsonToColumns(json, schema);

            expected[row.TagName] = props;
        }
        return expected;
    }

    /// <summary>
    /// value_jsonb（键 = 原始 FieldName）→ Tag 列值（按快照 SourceField 精确回源）。
    /// 值为 null 的列不写（Nebula 缺省即 NULL）。
    /// </summary>
    private static Dictionary<string, object?> MapJsonToColumns(
        JsonDocument json, TagProjectionSchema schema)
    {
        var root = json.RootElement;
        var props = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var col in schema.Columns)
        {
            if (!root.TryGetProperty(col.SourceField, out var el)) continue;
            var value = ConvertJsonValue(el, col.NebulaType);
            if (value is not null)
                props[col.Name] = new TypedValue(col.NebulaType, value);
        }
        return props;
    }

    /// <summary>JSON 值 → .NET 值（date/time/timestamp 按 nGQL 字面量所需形态）。</summary>
    private static object? ConvertJsonValue(JsonElement el, string nebulaType)
    {
        if (el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        try
        {
            return nebulaType switch
            {
                "string" => el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString(),
                "int64" => el.GetInt64(),
                "double" => el.GetDecimal(),
                "bool" => el.GetBoolean(),
                "date" => DateOnly.Parse(el.GetString()!),
                "time" => TimeOnly.Parse(el.GetString()!),
                "timestamp" => ParseToEpochSeconds(el.GetString()!),
                _ => null
            };
        }
        catch (FormatException)
        {
            return null; // 脏值不阻塞同步：该列写 NULL
        }
    }

    private static long ParseToEpochSeconds(string iso)
        => DateTimeOffset.Parse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind)
            .ToUnixTimeSeconds();

    /// <summary>类型化值（写 nGQL 字面量时按 Nebula 类型选择构造器）。</summary>
    private sealed record TypedValue(string NebulaType, object Value);

    /// <summary>INSERT VERTEX 单 Tag（覆盖语义；date/time/timestamp 用 nGQL 构造函数字面量）。</summary>
    private static string BuildInsertNgql(string vid, string tagName, Dictionary<string, object?> props)
    {
        if (props.Count == 0)
            return $"INSERT VERTEX `{tagName}`() VALUES \"{vid}\":()";

        var names = string.Join(",", props.Keys.Select(k => $"`{k}`"));
        var values = string.Join(",", props.Values.Select(FormatLiteral));
        return $"INSERT VERTEX `{tagName}`({names}) VALUES \"{vid}\":({values})";
    }

    private static string FormatLiteral(object? v) => v switch
    {
        null => "NULL",
        TypedValue { NebulaType: "date", Value: DateOnly d } => $"date(\"{d:yyyy-MM-dd}\")",
        TypedValue { NebulaType: "time", Value: TimeOnly t } => $"time(\"{t:HH:mm:ss}\")",
        TypedValue { NebulaType: "timestamp", Value: long epoch } => $"timestamp({epoch})",
        TypedValue { NebulaType: "string", Value: string s } => $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"",
        TypedValue { NebulaType: "int64", Value: long l } => l.ToString(),
        TypedValue { NebulaType: "double", Value: decimal dec } => dec.ToString(System.Globalization.CultureInfo.InvariantCulture),
        TypedValue { NebulaType: "bool", Value: bool b } => b ? "true" : "false",
        _ => "NULL"
    };

    /// <summary>读取顶点当前实际挂的 Tag 名集（FETCH PROP ON * 返回 vertex 字符串，解析 :tag{ 模式）。</summary>
    private async Task<HashSet<string>> FetchVertexTagsAsync(string space, string vid, CancellationToken ct)
    {
        var result = await _client.QueryAsync(space,
            $"FETCH PROP ON * \"{vid}\" YIELD vertex AS v", ct);
        var tags = new HashSet<string>(StringComparer.Ordinal);
        var raw = result.Data?
            .Select(r => r.TryGetValue("v", out var v) ? v.GetString() : null)
            .FirstOrDefault(s => !string.IsNullOrEmpty(s));
        if (raw is null) return tags;

        var idx = 0;
        while ((idx = raw.IndexOf(':', idx)) >= 0)
        {
            var start = idx + 1;
            var end = start;
            while (end < raw.Length && (char.IsLetterOrDigit(raw[end]) || raw[end] == '_')) end++;
            if (end > start && end < raw.Length && raw[end] == '{')
                tags.Add(raw[start..end]);
            idx = end;
        }
        return tags;
    }
}

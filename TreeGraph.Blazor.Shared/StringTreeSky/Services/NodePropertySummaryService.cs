using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using TreeGraph.Blazor.Shared.NodeEav.Services;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Blazor.Shared.StringTreeSky.Services;

/// <summary>
/// 节点属性摘要服务（多棵树感知）。
///
/// 缓存键：(EntityType, NodeId)
///   - 同一节点在 "StringTreeNode" 与 "StringTreeNode:products" 下摘要独立
///
/// 依赖 NodeSchemaCache 获取 Schema（共享缓存）。
/// Scoped 生命周期：每个 Circuit 一份缓存。
/// </summary>
public class NodePropertySummaryService
{
    private readonly EavApiClient _api;
    private readonly StringTreeSkyOptions _options;
    private readonly NodeSchemaCache _schemaCache;

    private readonly ConcurrentDictionary<(string EntityType, string NodeId), string?> _cache = new();

    public NodePropertySummaryService(
        EavApiClient api,
        StringTreeSkyOptions options,
        NodeSchemaCache schemaCache)
    {
        _api = api;
        _options = options;
        _schemaCache = schemaCache;
    }

    public bool IsEnabled => _options.SummaryAttributeNames.Count > 0;

    /// <summary>获取节点摘要。</summary>
    public async Task<string?> GetSummaryAsync(
        string entityType, string nodeId, CancellationToken ct = default)
    {
        if (!IsEnabled || string.IsNullOrEmpty(nodeId) || string.IsNullOrWhiteSpace(entityType))
            return null;

        var key = (entityType, nodeId);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        try
        {
            var schema = await _schemaCache.GetAsync(entityType, ct);
            if (schema.Count == 0)
            {
                _cache[key] = null;
                return null;
            }

            var entity = await _api.GetEntityAsync(
                entityType, nodeId, originalUnits: false, ct);

            var summary = BuildSummary(entity?.Properties, schema);
            _cache[key] = summary;
            return summary;
        }
        catch
        {
            _cache[key] = null;
            return null;
        }
    }

    /// <summary>批量获取（并发）。</summary>
    public async Task<IReadOnlyDictionary<string, string?>> GetSummariesAsync(
        string entityType, IEnumerable<string> nodeIds, CancellationToken ct = default)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(entityType))
            return new Dictionary<string, string?>();

        var ids = nodeIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var tasks = ids.Select(async id =>
            (id, summary: await GetSummaryAsync(entityType, id, ct)));
        var results = await Task.WhenAll(tasks);

        return results.ToDictionary(r => r.id, r => r.summary);
    }

    /// <summary>失效单个节点的摘要。</summary>
    public void Invalidate(string entityType, string nodeId)
        => _cache.TryRemove((entityType, nodeId), out _);

    /// <summary>失效指定 EntityType 下所有摘要。</summary>
    public void InvalidateEntityType(string entityType)
    {
        var keys = _cache.Keys.Where(k => k.EntityType == entityType).ToList();
        foreach (var key in keys) _cache.TryRemove(key, out _);
    }

    /// <summary>失效全部（含 Schema 缓存）。</summary>
    public void InvalidateAll()
    {
        _cache.Clear();
        _schemaCache.InvalidateAll();
    }

    // ============ 内部（保持增强项 2 的逻辑不变） ============

    private string? BuildSummary(
        IReadOnlyDictionary<string, JsonElement>? props,
        IReadOnlyList<AttributeSchemaDto> schema)
    {
        if (props is null || props.Count == 0) return null;

        var parts = new List<string>(_options.SummaryAttributeNames.Count);

        foreach (var name in _options.SummaryAttributeNames)
        {
            var attr = schema.FirstOrDefault(a =>
                string.Equals(a.AttributeName, name, StringComparison.Ordinal));
            if (attr is null) continue;
            if (!props.TryGetValue(name, out var elem)) continue;

            var text = FormatValue(elem, attr);
            if (string.IsNullOrWhiteSpace(text)) continue;

            parts.Add($"{attr.DisplayName}: {text}");
        }

        if (parts.Count == 0) return null;

        var joined = string.Join(" · ", parts);
        var max = _options.SummaryMaxLength;
        if (max > 0 && joined.Length > max)
            joined = joined[..max] + "…";

        return joined;
    }

    private static string FormatValue(JsonElement elem, AttributeSchemaDto attr)
    {
        if (elem.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return "";

        return attr.DataType switch
        {
            EavDataTypes.String =>
                elem.ValueKind == JsonValueKind.String ? elem.GetString() ?? "" : elem.GetRawText(),

            EavDataTypes.Int or EavDataTypes.Decimal => FormatNumeric(elem),

            EavDataTypes.Bool =>
                elem.ValueKind == JsonValueKind.True ? "是"
                : elem.ValueKind == JsonValueKind.False ? "否"
                : "",

            EavDataTypes.Date =>
                elem.ValueKind == JsonValueKind.String ? elem.GetString() ?? "" : "",

            EavDataTypes.Datetime =>
                elem.TryGetDateTime(out var dt)
                    ? dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                    : "",

            EavDataTypes.Time =>
                elem.ValueKind == JsonValueKind.String ? elem.GetString() ?? "" : "",

            EavDataTypes.SingleChoice =>
                elem.ValueKind == JsonValueKind.String
                    ? elem.GetString() ?? ""
                    : (elem.ValueKind == JsonValueKind.Object
                        && elem.TryGetProperty("value", out var v)
                        && v.ValueKind == JsonValueKind.String
                        ? v.GetString() ?? ""
                        : ""),

            _ => ""
        };
    }

    private static string FormatNumeric(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.Number && elem.TryGetDecimal(out var d))
            return d.ToString("0.##", CultureInfo.InvariantCulture);

        if (elem.ValueKind == JsonValueKind.Object
            && elem.TryGetProperty("value", out var v)
            && v.ValueKind == JsonValueKind.Number
            && v.TryGetDecimal(out var dv))
            return dv.ToString("0.##", CultureInfo.InvariantCulture);

        return "";
    }
}

using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.NodeEavSky;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>
/// 加载单节点子树并渲染 EAV 文本。供 MafWfApi 的 NodeExpandService 调用。
/// </summary>
public sealed class SubtreeService
{
    private const int MaxAttrValueLen = 200;
    private const int MaxTableRows = 5;

    private readonly TreeGraphDbContext _db;
    private readonly IAttributeCache _attrCache;
    private readonly IOptionSetCache _optionSetCache;
    private readonly ICompositeTypeCache _compositeCache;
    private readonly ICustomTableCache _customTableCache;
    private readonly ILogger<SubtreeService> _logger;

    public SubtreeService(
        TreeGraphDbContext db,
        IAttributeCache attrCache,
        IOptionSetCache optionSetCache,
        ICompositeTypeCache compositeCache,
        ICustomTableCache customTableCache,
        ILogger<SubtreeService> logger)
    {
        _db = db;
        _attrCache = attrCache;
        _optionSetCache = optionSetCache;
        _compositeCache = compositeCache;
        _customTableCache = customTableCache;
        _logger = logger;
    }

    public async Task<SubtreeLoadResult> LoadAsync(
        string hostType, string rootId, int maxDepth, int maxNodes,
        CancellationToken ct)
    {
        var raw = hostType switch
        {
            "SpaceTree" => await LoadRawSpaceTreeAsync(rootId, maxDepth, maxNodes, ct),
            "UnitTree" => await LoadRawUnitTreeAsync(rootId, maxDepth, maxNodes, ct),
            "CategoryTree" => await LoadRawCategoryTreeAsync(rootId, maxDepth, maxNodes, ct),
            "KnowledgeTree" => await LoadRawKnowledgeTreeAsync(rootId, maxDepth, maxNodes, ct),
            _ => new List<RawNode>()
        };

        if (raw.Count == 0)
            return new SubtreeLoadResult(null, 0, false);

        var truncated = raw.Count >= maxNodes;

        // 批量加载 EAV（避免逐节点查询）
        var nodeIds = raw.Select(n => n.Id).ToList();
        var eavMap = await LoadEavBatchAsync(nodeIds, ct);

        // 树形组装
        var nodeMap = raw.ToDictionary(
            n => n.Id,
            n => new SubtreeNodeDto(
                Id: n.Id,
                Name: n.Name,
                Path: null,
                EavRendered: eavMap.GetValueOrDefault(n.Id),
                Children: new List<SubtreeNodeDto>()));

        if (!nodeMap.TryGetValue(rootId, out var root))
            return new SubtreeLoadResult(null, raw.Count, truncated);

        foreach (var n in raw)
        {
            if (n.ParentId is null || n.Id == rootId) continue;
            if (nodeMap.TryGetValue(n.ParentId, out var parent)
                && nodeMap.TryGetValue(n.Id, out var child))
            {
                parent.Children.Add(child);
            }
        }

        // 根节点补 Path
        var path = await BuildPathAsync(hostType, rootId, ct);
        root = root with { Path = path };

        return new SubtreeLoadResult(root, raw.Count, truncated);
    }

    // ═══════════════════════════════════════════════════════
    // 递归 CTE
    // ═══════════════════════════════════════════════════════

    private Task<List<RawNode>> LoadRawSpaceTreeAsync(
        string rootId, int maxDepth, int maxNodes, CancellationToken ct)
        => _db.Database.SqlQueryRaw<RawNode>("""
            WITH RECURSIVE subtree AS (
                SELECT id, name, parent_id, 0 AS depth
                FROM space_trees WHERE id = {0}
                UNION ALL
                SELECT s.id, s.name, s.parent_id, t.depth + 1
                FROM space_trees s
                JOIN subtree t ON s.parent_id = t.id
                WHERE t.depth < {1}
            )
            SELECT id AS "Id", name AS "Name", parent_id AS "ParentId", depth AS "Depth"
            FROM subtree ORDER BY depth, name LIMIT {2}
            """, rootId, maxDepth, maxNodes)
            .ToListAsync(ct);

    private Task<List<RawNode>> LoadRawUnitTreeAsync(
        string rootId, int maxDepth, int maxNodes, CancellationToken ct)
        => _db.Database.SqlQueryRaw<RawNode>("""
            WITH RECURSIVE subtree AS (
                SELECT id, name, parent_id, 0 AS depth
                FROM unit_trees WHERE id = {0}
                UNION ALL
                SELECT s.id, s.name, s.parent_id, t.depth + 1
                FROM unit_trees s
                JOIN subtree t ON s.parent_id = t.id
                WHERE t.depth < {1}
            )
            SELECT id AS "Id", name AS "Name", parent_id AS "ParentId", depth AS "Depth"
            FROM subtree ORDER BY depth, name LIMIT {2}
            """, rootId, maxDepth, maxNodes)
            .ToListAsync(ct);

    private Task<List<RawNode>> LoadRawCategoryTreeAsync(
        string rootId, int maxDepth, int maxNodes, CancellationToken ct)
        => _db.Database.SqlQueryRaw<RawNode>("""
            WITH RECURSIVE subtree AS (
                SELECT id, name, parent_id, 0 AS depth
                FROM category_trees WHERE id = {0}
                UNION ALL
                SELECT c.id, c.name, c.parent_id, t.depth + 1
                FROM category_trees c
                JOIN subtree t ON c.parent_id = t.id
                WHERE t.depth < {1}
            )
            SELECT id AS "Id", name AS "Name", parent_id AS "ParentId", depth AS "Depth"
            FROM subtree ORDER BY depth, name LIMIT {2}
            """, rootId, maxDepth, maxNodes)
            .ToListAsync(ct);

    private Task<List<RawNode>> LoadRawKnowledgeTreeAsync(
        string rootId, int maxDepth, int maxNodes, CancellationToken ct)
        => _db.Database.SqlQueryRaw<RawNode>("""
            WITH RECURSIVE subtree AS (
                SELECT id, name, parent_id, 0 AS depth
                FROM knowledge_trees WHERE id = {0}
                UNION ALL
                SELECT c.id, c.name, c.parent_id, t.depth + 1
                FROM knowledge_trees c
                JOIN subtree t ON c.parent_id = t.id
                WHERE t.depth < {1}
            )
            SELECT id AS "Id", name AS "Name", parent_id AS "ParentId", depth AS "Depth"
            FROM subtree ORDER BY depth, name LIMIT {2}
            """, rootId, maxDepth, maxNodes)
            .ToListAsync(ct);

    // ═══════════════════════════════════════════════════════
    // 批量加载 EAV（返回 nodeId → 渲染文本）
    // ═══════════════════════════════════════════════════════

    private async Task<Dictionary<string, string>> LoadEavBatchAsync(
        List<string> nodeIds, CancellationToken ct)
    {
        var result = new Dictionary<string, string>();
        if (nodeIds.Count == 0) return result;

        // 1. 批量查 inode_entity
        var mappings = await _db.InodeEntities
            .Where(m => nodeIds.Contains(m.InodeId))
            .OrderBy(m => m.InodeId).ThenBy(m => m.EntityType)
            .AsNoTracking()
            .ToListAsync(ct);

        if (mappings.Count == 0) return result;

        // 2. 按 inodeId 分组
        var byInode = mappings.GroupBy(m => m.InodeId).ToDictionary(g => g.Key, g => g.ToList());

        // 3. 批量查 entityType 显示名
        var entityTypes = mappings.Select(m => m.EntityType).Distinct().ToList();
        var typeDisplayMap = await _db.EntityTypes
            .Where(t => entityTypes.Contains(t.EntityType))
            .AsNoTracking()
            .Select(t => new { t.EntityType, t.DisplayName })
            .ToDictionaryAsync(t => t.EntityType, t => t.DisplayName, ct);

        foreach (var (inodeId, entities) in byInode)
        {
            var sb = new StringBuilder();

            foreach (var m in entities)
            {
                var typeDisplay = typeDisplayMap.GetValueOrDefault(m.EntityType, m.EntityType);
                var lines = await RenderEntityAsync(
                    m.EntityId, m.EntityType, ct);

                if (lines.Count > 0)
                {
                    sb.Append("  【").Append(typeDisplay).AppendLine("】");
                    foreach (var line in lines)
                        sb.Append("    ").AppendLine(line);
                }
            }

            if (sb.Length > 0)
                result[inodeId] = sb.ToString().TrimEnd();
        }

        return result;
    }

    private async Task<List<string>> RenderEntityAsync(
        string entityId, string entityType, CancellationToken ct)
    {
        var defs = _attrCache.GetDefinitions(entityType)
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.AttributeName, StringComparer.Ordinal)
            .ToList();

        if (defs.Count == 0) return new List<string>();

        var values = await _db.AttributeValues
            .Where(v => v.EntityId == entityId && v.EntityType == entityType)
            .AsNoTracking()
            .ToDictionaryAsync(v => v.AttributeId, ct);

        var locations = await _db.LocationAttributeValues
            .Where(l => l.EntityId == entityId && l.EntityType == entityType)
            .AsNoTracking()
            .ToDictionaryAsync(l => l.AttributeId, ct);

        var lines = new List<string>();

        foreach (var def in defs)
        {
            var text = await FormatAttributeAsync(
                def, values, locations, entityId, entityType, ct);

            if (!string.IsNullOrWhiteSpace(text))
                lines.Add($"{def.DisplayName}：{text}");
        }

        return lines;
    }

    // ═══════════════════════════════════════════════════════
    // 属性渲染（覆盖 14 种类型）
    // ═══════════════════════════════════════════════════════

    private async Task<string?> FormatAttributeAsync(
        AttributeDefinition def,
        Dictionary<string, AttributeValue> values,
        Dictionary<string, LocationAttributeValue> locations,
        string entityId, string entityType,
        CancellationToken ct)
    {
        // location：独立表
        if (def.DataType == EavDataTypes.Location)
        {
            if (!locations.TryGetValue(def.AttributeId, out var loc)) return null;
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(loc.Label)) parts.Add(loc.Label);
            if (!string.IsNullOrEmpty(loc.Address)) parts.Add(loc.Address);
            parts.Add($"({loc.Latitude:F4}, {loc.Longitude:F4})");
            return string.Join(" ", parts);
        }

        // table：查子表
        if (def.DataType == EavDataTypes.Table)
        {
            if (def.RefTableDefinitionId is null) return null;
            return await FormatTableAsync(entityId, entityType, def, ct);
        }

        // 其余走 AttributeValue
        if (!values.TryGetValue(def.AttributeId, out var v)) return null;

        return def.DataType switch
        {
            EavDataTypes.String => Truncate(v.ValueString, MaxAttrValueLen),
            EavDataTypes.SingleChoice => await FormatSingleChoiceAsync(v.ValueString, def, ct),

            // ★ Int 带单位时读 ValueInt（文档原版本误读 ValueDecimal，已修复）
            EavDataTypes.Int when def.UnitId is not null
                => $"{v.ValueInt} {await GetUnitNameAsync(def.UnitId, ct)}",
            EavDataTypes.Int => v.ValueInt?.ToString(),

            EavDataTypes.Decimal when def.UnitId is not null
                => $"{v.ValueDecimal} {await GetUnitNameAsync(def.UnitId, ct)}",
            EavDataTypes.Decimal => v.ValueDecimal?.ToString(),

            EavDataTypes.Bool => v.ValueBool == true ? "是" : "否",
            EavDataTypes.Datetime => v.ValueDatetime?.ToString("yyyy-MM-dd HH:mm"),
            EavDataTypes.Date => v.ValueDateOnly?.ToString("yyyy-MM-dd"),
            EavDataTypes.Time => v.ValueTime?.ToString("HH:mm:ss"),

            EavDataTypes.Json => FormatJson(v.ValueJsonb, 300),
            EavDataTypes.Composite => FormatComposite(v.ValueJsonb, def.RefCompositeTypeId),
            EavDataTypes.File => FormatFile(v.ValueFileMeta),

            _ => null
        };
    }

    private async Task<string?> FormatSingleChoiceAsync(
        string? value, AttributeDefinition def, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (def.RefOptionSetId is null) return value;

        try
        {
            var set = _optionSetCache.GetSet(def.RefOptionSetId);
            var item = set.Items.FirstOrDefault(i => i.Value == value);
            return item?.Label ?? value;
        }
        catch (KeyNotFoundException) { return value; }
    }

    private string FormatComposite(JsonDocument? json, string? compositeTypeId)
    {
        if (json is null || compositeTypeId is null) return "";
        try
        {
            var typeDef = _compositeCache.GetType(compositeTypeId);
            var root = json.RootElement;
            var parts = new List<string>();

            foreach (var field in typeDef.Fields
                .Where(f => !f.IsDeleted)
                .OrderBy(f => f.DisplayOrder))
            {
                if (!root.TryGetProperty(field.FieldName, out var el)) continue;
                if (el.ValueKind == JsonValueKind.Null) continue;

                var val = FormatJsonElement(el);
                if (!string.IsNullOrEmpty(val))
                    parts.Add($"{field.DisplayName}={val}");
            }

            return string.Join(", ", parts);
        }
        catch (KeyNotFoundException) { return ""; }
    }

    private async Task<string> FormatTableAsync(
        string entityId, string entityType,
        AttributeDefinition def, CancellationToken ct)
    {
        CustomTableDefinition table;
        try { table = _customTableCache.GetTable(def.RefTableDefinitionId!); }
        catch { return ""; }

        var rows = await _db.CustomTableRows
            .Where(r => r.ParentEntityId == entityId
                     && r.ParentEntityType == entityType
                     && r.AttributeId == def.AttributeId)
            .OrderBy(r => r.RowOrder)
            .Take(MaxTableRows)
            .AsNoTracking()
            .ToListAsync(ct);

        if (rows.Count == 0) return "";

        var colDefs = table.Columns.Where(c => !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder).ToList();

        var lines = new List<string> { "" };

        foreach (var row in rows)
        {
            var root = row.RowData.RootElement;
            var cells = new List<string>();
            foreach (var col in colDefs)
            {
                if (!root.TryGetProperty(col.ColumnName, out var el)) continue;
                if (el.ValueKind == JsonValueKind.Null) continue;
                var val = FormatJsonElement(el);
                if (!string.IsNullOrEmpty(val))
                    cells.Add($"{col.DisplayName}：{val}");
            }
            if (cells.Count > 0)
                lines.Add("    - " + string.Join(" | ", cells));
        }

        var total = await _db.CustomTableRows.CountAsync(
            r => r.ParentEntityId == entityId
              && r.ParentEntityType == entityType
              && r.AttributeId == def.AttributeId, ct);

        if (total > MaxTableRows)
            lines.Add($"    （共 {total} 行，仅展示前 {MaxTableRows} 行）");

        return string.Join("\n", lines);
    }

    private static string FormatFile(JsonDocument? fileMeta)
    {
        if (fileMeta is null) return "";
        var root = fileMeta.RootElement;
        var name = root.TryGetProperty("fileName", out var n) ? n.GetString() : null;
        var ver = root.TryGetProperty("version", out var v) && v.TryGetInt32(out var vi)
            ? vi : (int?)null;
        return ver is null ? name ?? "" : $"{name}（v{ver}）";
    }

    private static string FormatJson(JsonDocument? json, int maxLen)
        => json is null ? "" : Truncate(json.RootElement.GetRawText(), maxLen) ?? "";

    private static string FormatJsonElement(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString() ?? "",
        JsonValueKind.Number => el.GetRawText(),
        JsonValueKind.True => "是",
        JsonValueKind.False => "否",
        JsonValueKind.Array => string.Join("、", el.EnumerateArray().Select(x => x.ToString())),
        JsonValueKind.Object => el.GetRawText(),
        _ => ""
    };

    private async Task<string> GetUnitNameAsync(string? unitId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(unitId)) return "";
        var unit = await _db.UnitTrees
            .Where(u => u.Id == unitId)
            .Select(u => new { u.Name, u.Abbreviation })
            .AsNoTracking().FirstOrDefaultAsync(ct);
        if (unit is null) return "";
        return string.IsNullOrEmpty(unit.Abbreviation) ? unit.Name : unit.Abbreviation;
    }

    private async Task<string> BuildPathAsync(
        string hostType, string nodeId, CancellationToken ct)
    {
        var parts = new List<string>();
        var current = nodeId;
        var depth = 0;

        while (!string.IsNullOrEmpty(current) && depth++ < 20)
        {
            string? name = null;
            string? parentId = null;

            switch (hostType)
            {
                case "SpaceTree":
                    var st = await _db.SpaceTrees.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == current, ct);
                    name = st?.Name; parentId = st?.ParentId; break;
                case "UnitTree":
                    var ut = await _db.UnitTrees.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == current, ct);
                    name = ut?.Name; parentId = ut?.ParentId; break;
                case "CategoryTree":
                    var ct2 = await _db.CategoryTrees.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == current, ct);
                    name = ct2?.Name; parentId = ct2?.ParentId; break;
                case "KnowledgeTree":
                    var kt = await _db.KnowledgeTrees.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == current, ct);
                    name = kt?.Name; parentId = kt?.ParentId; break;
            }

            if (name is null) break;
            parts.Insert(0, name);
            current = parentId;
        }

        return string.Join(" / ", parts);
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";

    private sealed record RawNode(string Id, string Name, string? ParentId, int Depth);
}

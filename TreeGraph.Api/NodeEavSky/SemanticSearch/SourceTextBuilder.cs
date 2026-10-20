using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.SpaceSky.Entities;
using TreeGraph.Shared.TreeEavSky.Entities;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>
/// 确定性宿主文本构造器。
///
/// 输入：inodeId + 当前 PostgreSQL 已提交状态
/// 输出：(text, hostType, spaceId, sourceHash)
///
/// 确定性要求：
///   - 同一份数据库状态，任意时刻、任意进程构造出的文本与 sourceHash 完全一致
///   - 属性按 AttributeDefinition.DisplayOrder 排序，与人工认知顺序一致
///   - 不依赖运行时随机因素（不含时间戳 / 随机 ID）
///
/// 幂等的基础：消费者收到重复 / 乱序消息时，重新构造的文本与
/// sourceHash 相同，upsert 不产生实际变化（Qdrant upsert 覆盖同 PointId）。
/// </summary>
public sealed class SourceTextBuilder
{
    /// <summary>当前节点视图模板版本。模板文案/标签词/截断/规范化规则变化必须升版并全量重算。</summary>
    public const string TemplateVersion = "v1";

    /// <summary>节点向量视图类型。</summary>
    public const string ViewType = "path_view";

    private const string PathLabel = "层级路径：";
    private const string SelfLabel = "当前节点：";
    private const string UnnamedNode = "未命名节点";
    private const string Ellipsis = "…";
    private const string PathSeparator = " > ";
    private const int MaxNameChars = 60;
    private const int MaxPathSegments = 8;
    private const int MaxTailSegments = 4;
    private const int MaxTextChars = 400;

    private readonly TreeGraphDbContext _db;

    public SourceTextBuilder(TreeGraphDbContext db) => _db = db;

    /// <summary>
    /// 为指定 iNode 构造宿主文本（节点 + 实体分离）。
    /// 返回：NodeText（纯节点名）+ EntityTexts（各实体独立文本）。
    /// 宿主范围：4 棵宿主树（空间/单位/分类/知识）+ 线路节点，均创建节点向量。
    /// </summary>
    public async Task<NodeSourceText?> BuildAsync(string inodeId, CancellationToken ct = default)
    {
        // 1. 查 inode 归属的全部实体
        var entityMounts = await _db.InodeEntities
            .AsNoTracking()
            .Where(m => m.InodeId == inodeId)
            .Select(m => new { m.EntityType, m.EntityId })
            .ToListAsync(ct);

        // 2. 解析宿主节点（Id 全局唯一，先中先得）
        var space = await _db.SpaceTrees
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == inodeId, ct);
        var unit = space is null
            ? await _db.UnitTrees.AsNoTracking().FirstOrDefaultAsync(n => n.Id == inodeId, ct)
            : null;
        var category = space is null && unit is null
            ? await _db.CategoryTrees.AsNoTracking().FirstOrDefaultAsync(n => n.Id == inodeId, ct)
            : null;
        var knowledge = space is null && unit is null && category is null
            ? await _db.KnowledgeTrees.AsNoTracking().FirstOrDefaultAsync(n => n.Id == inodeId, ct)
            : null;
        var lineNode = space is null && unit is null && category is null && knowledge is null
            ? await _db.LineNodes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == inodeId, ct)
            : null;

        if (space is null && unit is null && category is null &&
            knowledge is null && lineNode is null && entityMounts.Count == 0)
            return null; // 既无实体也不在任何宿主 → 删除向量

        // 3. 构造节点文本（path_view 两段式：层级路径 + 当前节点）。
        // space 节点（Id 存在于 spaces 空间实体列表内，代表空间本身）不创建向量。
        var isSpaceNode = space is not null &&
            await _db.Spaces.AsNoTracking().AnyAsync(s => s.Id == inodeId, ct);
        var nodeName = isSpaceNode
            ? null
            : space?.Name ?? unit?.Name ?? category?.Name ?? knowledge?.Name ?? lineNode?.Name;

        // 祖先链：4 棵宿主树内沿 ParentId 上溯（线路节点无父子结构，祖先为空）
        var ancestorNames = new List<string>();
        var ancestorIds = new List<string>();
        if (nodeName is not null)
        {
            if (space is not null)
                (ancestorNames, ancestorIds) =
                    await BuildAncestorChainAsync(_db.SpaceTrees, space.ParentId, ct);
            else if (unit is not null)
                (ancestorNames, ancestorIds) =
                    await BuildAncestorChainAsync(_db.UnitTrees, unit.ParentId, ct);
            else if (category is not null)
                (ancestorNames, ancestorIds) =
                    await BuildAncestorChainAsync(_db.CategoryTrees, category.ParentId, ct);
            else if (knowledge is not null)
                (ancestorNames, ancestorIds) =
                    await BuildAncestorChainAsync(_db.KnowledgeTrees, knowledge.ParentId, ct);
        }

        var selfName = NormalizeName(nodeName);
        var nodeText = nodeName is null ? "" : BuildNodeText(ancestorNames, selfName);
        var nodeHash = ComputeHash(TemplateVersion + "\n" + nodeText);

        // 4. 宿主类型与归属空间（检索按 space_id 过滤）
        var hostType = space is not null ? "space_tree"
            : unit is not null ? "unit_tree"
            : category is not null ? "category_tree"
            : knowledge is not null ? "knowledge_tree"
            : "line_node";
        var spaceId = await ResolveSpaceIdAsync(space, unit, category, knowledge, inodeId, ct);

        // 5. 构造各实体独立文本
        var entityTexts = new List<EntitySourceText>();
        foreach (var mount in entityMounts)
        {
            var entityText = await BuildEntityTextAsync(
                mount.EntityType, mount.EntityId, ct);
            if (entityText is not null)
                entityTexts.Add(entityText);
        }

        // payload 路径快照：完整路径（含自身，段数不截断）+ 全路径 Id + 深度（根=0）
        var pathIds = new List<string>(ancestorIds) { inodeId };
        var path = nodeName is null
            ? ""
            : string.Join(PathSeparator,
                ancestorNames.Append(selfName).Select(n => Clip(n, MaxNameChars)));

        return new NodeSourceText(
            inodeId, hostType, spaceId,
            nodeText, nodeHash, entityTexts,
            path, pathIds, ancestorIds.Count);
    }

    /// <summary>
    /// 解析节点向量的归属空间（payload space_id，供按空间检索过滤）：
    ///   空间树节点 → 自身或最近的空间祖先（Id ∈ spaces）；
    ///   单位/分类/知识树节点 → 对应类型空间（当前每类型单空间假设，与对账 ResolveScope 口径一致）；
    ///   线路节点 → 无空间归属，归 "default"（仅全局检索可见）。
    /// 无对应空间时归 "default"。
    /// </summary>
    private async Task<string> ResolveSpaceIdAsync(
        SpaceTree? space,
        UnitTree? unit,
        CategoryTree? category,
        KnowledgeTree? knowledge,
        string inodeId,
        CancellationToken ct)
    {
        var spaceIds = (await _db.Spaces.AsNoTracking()
                .Select(s => s.Id)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        if (space is not null)
        {
            if (spaceIds.Contains(inodeId)) return inodeId;

            // 沿 ParentId 上溯找最近的空间祖先
            var parentMap = (await _db.SpaceTrees.AsNoTracking()
                    .Select(n => new { n.Id, n.ParentId })
                    .ToListAsync(ct))
                .ToDictionary(n => n.Id, n => n.ParentId, StringComparer.Ordinal);
            var current = space.ParentId;
            var guard = new HashSet<string>(StringComparer.Ordinal);
            while (current is not null && guard.Add(current))
            {
                if (spaceIds.Contains(current)) return current;
                parentMap.TryGetValue(current, out current);
            }
            return "default";
        }

        SpaceType? spaceType = unit is not null ? SpaceType.Unit
            : category is not null ? SpaceType.Type
            : knowledge is not null ? SpaceType.Knowledge
            : null;
        if (spaceType is null) return "default"; // 线路节点

        return await _db.Spaces.AsNoTracking()
            .Where(s => s.Type == spaceType.Value)
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(ct) ?? "default";
    }

    /// <summary>
    /// 在单棵宿主树内沿 ParentId 上溯收集祖先（归一化名称 + Id，根在前）。
    /// 整表加载后内存上溯；父链断裂或成环时按已收集部分返回，保证确定性。
    /// </summary>
    private async Task<(List<string> Names, List<string> Ids)> BuildAncestorChainAsync<T>(
        DbSet<T> set, string? parentId, CancellationToken ct) where T : class
    {
        var nodes = await set.AsNoTracking()
            .Select(n => new
            {
                Id = EF.Property<string>(n, "Id"),
                ParentId = EF.Property<string>(n, "ParentId"),
                Name = EF.Property<string>(n, "Name")
            })
            .ToListAsync(ct);
        var map = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);

        var names = new List<string>();
        var ids = new List<string>();
        var guard = new HashSet<string>(StringComparer.Ordinal);
        var current = parentId;
        while (current is not null && map.TryGetValue(current, out var node) && guard.Add(current))
        {
            names.Insert(0, NormalizeName(node.Name));
            ids.Insert(0, node.Id);
            current = node.ParentId;
        }

        return (names, ids);
    }

    /// <summary>
    /// path_view 两段式节点文本：层级路径行（有祖先时）+ 当前节点行。
    /// 祖先 ≤8 层全保留，超过保留 根 + … + 最近 4 层；
    /// 整条超 400 字符时逐步收短祖先路径（保根 + 最近层），当前节点行永不裁剪。
    /// </summary>
    private static string BuildNodeText(List<string> ancestorNames, string selfName)
    {
        var selfLine = SelfLabel + Clip(selfName, MaxNameChars);
        if (ancestorNames.Count == 0) return selfLine;

        var keep = ancestorNames.Count <= MaxPathSegments
            ? ancestorNames.Count
            : MaxTailSegments;
        var text = BuildPathLine(ancestorNames, keep) + "\n" + selfLine;
        while (text.Length > MaxTextChars && keep > 1)
        {
            keep--;
            text = BuildPathLine(ancestorNames, keep) + "\n" + selfLine;
        }

        return text.Length <= MaxTextChars ? text : selfLine;
    }

    /// <summary>构造层级路径行；keep 小于段数时保留 根 + … + 最近 keep 层。</summary>
    private static string BuildPathLine(List<string> ancestorNames, int keep)
    {
        var segs = keep >= ancestorNames.Count
            ? ancestorNames
            : ancestorNames.Take(1)
                .Append(Ellipsis)
                .Concat(ancestorNames.Skip(ancestorNames.Count - keep))
                .ToList();
        return PathLabel + string.Join(PathSeparator, segs.Select(s => Clip(s, MaxNameChars)));
    }

    /// <summary>
    /// 名称规范化（进入向量文本前）：去首尾空白（含全角空格）、连续空白压缩为
    /// 单个半角空格、删除其余控制字符；空/纯空白名回退占位符。
    /// 不改大小写、不翻译、不删标点。
    /// </summary>
    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return UnnamedNode;

        var sb = new StringBuilder(name.Length);
        var pendingSpace = false;
        foreach (var ch in name)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0) pendingSpace = true;
                continue;
            }
            if (ch < ' ') continue; // 其余控制字符（\u0000-\u001F）删除
            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }
            sb.Append(ch);
        }

        return sb.Length == 0 ? UnnamedNode : sb.ToString();
    }

    /// <summary>超长段截断：保留前 <paramref name="maxChars"/> 字符并追加省略号。</summary>
    private static string Clip(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[..maxChars] + Ellipsis;

    /// <summary>
    /// 构造单个实体的独立文本。
    /// </summary>
    private async Task<EntitySourceText?> BuildEntityTextAsync(
        string entityType, string entityId, CancellationToken ct)
    {
        var lines = new List<string> { $"类型：{entityType}" };

        var attrs = _db.AttributeCatalog
            .Where(a => a.EntityType == entityType && !a.IsDeleted)
            .ToList();
        var defs = attrs
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.AttributeId, StringComparer.Ordinal)
            .ToList();

        var values = await _db.AttributeValues
            .AsNoTracking()
            .Where(v => v.EntityType == entityType && v.EntityId == entityId)
            .ToListAsync(ct);

        var byAttrId = values.ToDictionary(v => v.AttributeId, v => v);

        foreach (var def in defs)
        {
            if (!byAttrId.TryGetValue(def.AttributeId, out var val))
                continue;

            var display = FormatValue(val);
            if (display is null) continue;
            lines.Add($"{def.DisplayName}：{display}");
        }

        // 自定义表子数据
        var tableRows = await _db.CustomTableRows
            .AsNoTracking()
            .Where(r => r.ParentEntityType == entityType &&
                        r.ParentEntityId == entityId)
            .OrderBy(r => r.RowOrder)
            .Take(50)
            .ToListAsync(ct);

        foreach (var row in tableRows)
        {
            var rowText = FormatTableRow(row.RowData);
            if (!string.IsNullOrWhiteSpace(rowText))
                lines.Add($"行：{rowText}");
        }

        if (lines.Count <= 1) return null; // 只有类型行，无实际内容

        var text = string.Join("；", lines);
        var hash = ComputeHash(text);

        return new EntitySourceText(entityId, entityType, text, hash);
    }

    private static string? FormatValue(NodeEavSky.Entities.AttributeValue v)
    {
        if (!string.IsNullOrWhiteSpace(v.ValueString)) return v.ValueString;
        if (v.ValueInt.HasValue) return v.ValueInt.Value.ToString();
        if (v.ValueDecimal.HasValue) return v.ValueDecimal.Value.ToString("G29");
        if (v.ValueBool.HasValue) return v.ValueBool.Value ? "是" : "否";
        if (v.ValueDatetime.HasValue) return v.ValueDatetime.Value.ToString("yyyy-MM-dd HH:mm");
        if (v.ValueJsonb is not null) return v.ValueJsonb.RootElement.GetRawText();
        return null;
    }

    private static string FormatTableRow(System.Text.Json.JsonDocument rowData)
    {
        var parts = new List<string>();
        foreach (var prop in rowData.RootElement.EnumerateObject())
        {
            var val = prop.Value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => prop.Value.GetString(),
                System.Text.Json.JsonValueKind.Number => prop.Value.GetRawText(),
                System.Text.Json.JsonValueKind.True => "是",
                System.Text.Json.JsonValueKind.False => "否",
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(val))
                parts.Add($"{prop.Name}={val}");
        }
        return string.Join("，", parts);
    }

    /// <summary>SHA256 内容指纹（十六进制小写）。</summary>
    public static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>节点级源文本：path_view 节点文本 + 路径快照 + 挂载的全部实体独立文本。</summary>
public sealed record NodeSourceText(
    string InodeId,
    string HostType,
    string SpaceId,
    string NodeText,
    string NodeHash,
    IReadOnlyList<EntitySourceText> EntityTexts,
    string Path,
    IReadOnlyList<string> PathIds,
    int Depth);

/// <summary>实体级源文本：独立于节点存在，仅通过 InodeId 建立路径关系。</summary>
public sealed record EntitySourceText(
    string EntityId,
    string EntityType,
    string Text,
    string Hash);

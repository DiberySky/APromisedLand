using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TreeGraph.Api.Data;
using TreeGraph.Shared.NodeEavSky.Dtos;
using TreeGraph.Shared.TreeEavSky.Entities;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>
/// 语义搜索服务：query → 向量 → Qdrant → 空间树反查（名称/父名/路径/根）。
///
/// 相关性门槛（两级，见 <see cref="SemanticSearchOptions"/>）：
///   1. 绝对下限：低于 MinScore 的命中不返回（请求 MinScore 优先，交给 Qdrant 原生过滤）
///   2. 相对容差：只保留 score ≥ (本轮最高分 − RelativeMargin) 的命中
/// 因相对门槛需要看到最高分，候选数先放大（topK × 倍数），过滤后再取 topK。
///
/// 路径回填：优先取 Qdrant payload 的 path/path_ids（向量化时刻的层级快照），
/// 缺失时 PG 反查兜底（SpaceTree 走 BuildPath，其余宿主走 BuildPgPathsAsync），
/// 五类宿主的 Path/RootId 均有值。
/// </summary>
public sealed class SearchService
{
    /// <summary>候选放大倍数：相对过滤需要多取候选。</summary>
    private const int CandidateMultiplier = 4;

    /// <summary>候选数下限（topK 较小时仍要有足够候选参与相对门槛判定）。</summary>
    private const int MinCandidates = 20;

    /// <summary>候选数上限（防止放大后单次拉取过多）。</summary>
    private const int MaxCandidates = 200;

    private readonly EmbeddingClient _embedding;
    private readonly QdrantVectorStore _store;
    private readonly TreeGraphDbContext _db;
    private readonly SemanticSearchOptions _options;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        EmbeddingClient embedding,
        QdrantVectorStore store,
        TreeGraphDbContext db,
        IOptions<SemanticSearchOptions> options,
        ILogger<SearchService> logger)
    {
        _embedding = embedding;
        _store = store;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SemanticSearchItemDto>> SearchAsync(
        string query, int topK, string? spaceId, float? minScore,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? attributeFilters = null,
        IReadOnlyDictionary<string, AttrRangeFilter>? attributeRanges = null,
        CancellationToken ct = default)
    {
        var vector = await _embedding.EmbedAsync(query, ct);

        // 绝对下限（请求值优先，钳制到 [0,1]）；候选放大后由 Qdrant 原生按此过滤
        var absoluteFloor = Math.Clamp(minScore ?? _options.MinScore, 0f, 1f);
        var candidateLimit = Math.Clamp(
            topK * CandidateMultiplier, MinCandidates, MaxCandidates);
        var hits = await _store.SearchAsync(
            vector, candidateLimit, spaceId, absoluteFloor,
            attributeFilters, attributeRanges, ct);

        // 相对门槛：只保留与本轮最高分相近的命中（剔除同批低相关噪声）
        if (_options.RelativeMargin > 0 && hits.Count > 0)
        {
            var topScore = hits.Max(h => h.Score);
            var relativeFloor = Math.Max(absoluteFloor, topScore - _options.RelativeMargin);
            var before = hits.Count;
            hits = hits.Where(h => h.Score >= relativeFloor).ToList();
            _logger.LogDebug(
                "相关度门槛：最低分={Floor} 最高分={Top} 相对下限={RelativeFloor} 候选 {Before}→{After}",
                absoluteFloor, topScore, relativeFloor, before, hits.Count);
        }

        // 节点优先过滤：节点已命中，则其自身及全部后代节点上的实体原始命中不再列出
        var nodeHitInodeIds = hits
            .Where(h => h.PointKind != "entity")
            .Select(h => h.InodeId)
            .ToHashSet(StringComparer.Ordinal);
        if (nodeHitInodeIds.Count > 0)
        {
            foreach (var descendantId in await LoadDescendantIdsAsync(nodeHitInodeIds, ct))
                nodeHitInodeIds.Add(descendantId);
            // 单位/分类/知识树宿主：在同树内展开后代（线路节点无父子结构）
            foreach (var descendantId in await LoadTreeDescendantIdsAsync(nodeHitInodeIds, ct))
                nodeHitInodeIds.Add(descendantId);
        }
        var filtered = hits
            .Where(h => h.PointKind != "entity" || !nodeHitInodeIds.Contains(h.InodeId))
            .Take(topK)
            .ToList();

        // 批量反查空间树节点（含全部祖先，BFS 逐层加载）
        var inodeIds = filtered.Select(h => h.InodeId).Distinct().ToList();
        var nodes = await LoadSpaceNodesWithAncestorsAsync(inodeIds, ct);
        // 非空间树宿主（单位/分类/知识树/线路节点）的名称反查
        var hostNames = await LoadHostNamesAsync(inodeIds, ct);
        // PG 兜底路径（payload path 缺失时回填，覆盖单位/分类/知识树/线路节点）
        var pgPaths = await BuildPgPathsAsync(inodeIds, ct);

        return filtered
            .Select(h =>
            {
                // 行命中与实体命中同构展示（MatchKind=Entity，EntityType=行所属实体类型）
                var isEntity = h.PointKind is "entity" or "row";
                var entityType = isEntity && !string.IsNullOrWhiteSpace(h.EntityType)
                    ? h.EntityType : null;

                // payload path 优先（向量化时刻的层级快照），PG 反查兜底
                var hasPayloadPath = !string.IsNullOrEmpty(h.Path);
                (string Path, string? RootId, string? ParentName) pg = default;
                if (pgPaths.TryGetValue(h.InodeId, out var found)) pg = found;

                if (nodes.TryGetValue(h.InodeId, out var node))
                {
                    var (pgPath, parentName, rootId) = BuildPath(node, nodes);
                    return new SemanticSearchItemDto(
                        InodeId: h.InodeId,
                        HostType: "SpaceTree",
                        NodeName: node.Name,
                        ParentName: parentName,
                        Path: hasPayloadPath ? h.Path : pgPath,
                        RootId: hasPayloadPath && h.PathIds is { Count: > 0 }
                            ? h.PathIds[0]
                            : rootId,
                        Score: (float)Math.Round(h.Score, 4),
                        MatchKind: isEntity ? "Entity" : "Node",
                        EntityType: entityType);
                }

                if (hostNames.TryGetValue(h.InodeId, out var host))
                {
                    return new SemanticSearchItemDto(
                        InodeId: h.InodeId,
                        HostType: host.HostType,
                        NodeName: host.Name,
                        ParentName: pg.ParentName,
                        Path: hasPayloadPath ? h.Path : pg.Path,
                        RootId: hasPayloadPath && h.PathIds is { Count: > 0 }
                            ? h.PathIds[0]
                            : pg.RootId,
                        Score: (float)Math.Round(h.Score, 4),
                        MatchKind: isEntity ? "Entity" : "Node",
                        EntityType: entityType);
                }

                // 未挂载任何宿主的命中（裸实体 / 待对账清理的孤儿向量）
                return new SemanticSearchItemDto(
                    InodeId: h.InodeId,
                    HostType: MapHostType(h.HostType),
                    NodeName: null,
                    ParentName: null,
                    Path: h.Path,
                    RootId: h.PathIds is { Count: > 0 } ? h.PathIds[0] : null,
                    Score: (float)Math.Round(h.Score, 4),
                    MatchKind: isEntity ? "Entity" : "Node",
                    EntityType: entityType);
            })
            // 同分命中按 (NodeName, Path) 稳定排序，保证结果顺序可复现
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.NodeName ?? "", StringComparer.Ordinal)
            .ThenBy(x => x.Path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 批量反查非空间树宿主的节点名称（单位树/分类树/知识树/线路节点）。
    /// 返回 inodeId → (名称, 前端契约 HostType)。
    /// </summary>
    private async Task<Dictionary<string, (string Name, string HostType)>> LoadHostNamesAsync(
        List<string> inodeIds, CancellationToken ct)
    {
        var result = new Dictionary<string, (string, string)>(StringComparer.Ordinal);

        foreach (var n in await _db.UnitTrees.AsNoTracking()
            .Where(n => inodeIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Name }).ToListAsync(ct))
            result[n.Id] = (n.Name, "UnitTree");

        foreach (var n in await _db.CategoryTrees.AsNoTracking()
            .Where(n => inodeIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Name }).ToListAsync(ct))
            result[n.Id] = (n.Name, "CategoryTree");

        foreach (var n in await _db.KnowledgeTrees.AsNoTracking()
            .Where(n => inodeIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Name }).ToListAsync(ct))
            result[n.Id] = (n.Name, "KnowledgeTree");

        foreach (var n in await _db.LineNodes.AsNoTracking()
            .Where(n => inodeIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Name }).ToListAsync(ct))
            result[n.Id] = (n.Name, "LineNode");

        return result;
    }

    /// <summary>
    /// PG 兜底路径解析（单位/分类/知识树 + 线路节点；SpaceTree 走 BuildPath）：
    /// payload path 缺失时回填 Path/RootId/ParentName。
    /// 整表加载后内存上溯，返回 inodeId → (Path 含自身, RootId, ParentName)。
    /// </summary>
    private async Task<Dictionary<string, (string Path, string? RootId, string? ParentName)>> BuildPgPathsAsync(
        List<string> inodeIds, CancellationToken ct)
    {
        var result = new Dictionary<string, (string, string?, string?)>(StringComparer.Ordinal);

        async Task LoadTree<T>(DbSet<T> set) where T : class
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

            foreach (var id in inodeIds)
            {
                if (result.ContainsKey(id) || !map.TryGetValue(id, out var start)) continue;

                var segs = new List<string>();
                string? parentName = null;
                var current = start;
                var guard = new HashSet<string>(StringComparer.Ordinal) { start.Id };
                while (true)
                {
                    segs.Add(current.Name ?? "");
                    if (current.ParentId is null ||
                        !map.TryGetValue(current.ParentId, out var parent) ||
                        !guard.Add(parent.Id))
                        break;
                    parentName ??= parent.Name;
                    current = parent;
                }

                segs.Reverse();
                result[id] = (string.Join(" / ", segs), current.Id, parentName);
            }
        }

        await LoadTree(_db.UnitTrees);
        await LoadTree(_db.CategoryTrees);
        await LoadTree(_db.KnowledgeTrees);

        // 线路节点：无层级，路径即自身名称，根为自身
        foreach (var n in await _db.LineNodes.AsNoTracking()
            .Where(n => inodeIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Name }).ToListAsync(ct))
            result[n.Id] = (n.Name ?? "", n.Id, null);

        return result;
    }

    /// <summary>
    /// 单位/分类/知识树内的后代展开（实体抑制用；线路节点无父子结构，不展开）。
    /// </summary>
    private async Task<List<string>> LoadTreeDescendantIdsAsync(
        IReadOnlyCollection<string> rootIds, CancellationToken ct)
    {
        var result = new List<string>();
        result.AddRange(await ExpandTreeDescendantsAsync(rootIds, _db.UnitTrees, ct));
        result.AddRange(await ExpandTreeDescendantsAsync(rootIds, _db.CategoryTrees, ct));
        result.AddRange(await ExpandTreeDescendantsAsync(rootIds, _db.KnowledgeTrees, ct));
        return result;
    }

    /// <summary>在单棵树表内 BFS 展开后代（不含传入根自身）。</summary>
    private async Task<List<string>> ExpandTreeDescendantsAsync<T>(
        IReadOnlyCollection<string> rootIds, Microsoft.EntityFrameworkCore.DbSet<T> set,
        CancellationToken ct) where T : class
    {
        var result = new List<string>();
        var known = new HashSet<string>(rootIds, StringComparer.Ordinal);
        var frontier = rootIds.ToList();

        while (frontier.Count > 0)
        {
            var children = await set.AsNoTracking()
                .Where(n => EF.Property<string>(n, "ParentId") != null &&
                            frontier.Contains(EF.Property<string>(n, "ParentId")))
                .Select(n => EF.Property<string>(n, "Id"))
                .ToListAsync(ct);

            frontier = children.Where(id => known.Add(id)).ToList();
            result.AddRange(frontier);
        }

        return result;
    }

    /// <summary>
    /// 加载目标节点及其全部祖先（BFS 逐层，层数 = 树高，通常 &lt; 10 次查询）。
    /// 返回以 Id 为键的字典，含目标与祖先节点。
    /// </summary>
    private async Task<Dictionary<string, SpaceTree>> LoadSpaceNodesWithAncestorsAsync(
        List<string> inodeIds, CancellationToken ct)
    {
        var map = await _db.SpaceTrees
            .AsNoTracking()
            .Where(n => inodeIds.Contains(n.Id))
            .ToDictionaryAsync(n => n.Id, ct);

        var frontier = map.Values
            .Select(n => n.ParentId)
            .Where(pid => pid is not null && !map.ContainsKey(pid))
            .Distinct()
            .ToList();

        while (frontier.Count > 0)
        {
            var parents = await _db.SpaceTrees
                .AsNoTracking()
                .Where(n => frontier.Contains(n.Id))
                .ToListAsync(ct);

            foreach (var p in parents)
                map[p.Id] = p;

            frontier = parents
                .Select(p => p.ParentId)
                .Where(pid => pid is not null && !map.ContainsKey(pid))
                .Distinct()
                .ToList();
        }

        return map;
    }

    /// <summary>
    /// 加载若干节点的全部后代节点 Id（BFS 逐层向下，用于节点命中后的子树实体过滤）。
    /// 不含传入的根 Id 本身。
    /// </summary>
    private async Task<List<string>> LoadDescendantIdsAsync(
        IReadOnlyCollection<string> rootIds, CancellationToken ct)
    {
        var result = new List<string>();
        var known = new HashSet<string>(rootIds, StringComparer.Ordinal);
        var frontier = rootIds.ToList();

        while (frontier.Count > 0)
        {
            var children = await _db.SpaceTrees
                .AsNoTracking()
                .Where(n => n.ParentId != null && frontier.Contains(n.ParentId))
                .Select(n => n.Id)
                .ToListAsync(ct);

            frontier = children.Where(id => known.Add(id)).ToList();
            result.AddRange(frontier);
        }

        return result;
    }

    /// <summary>
    /// 由祖先链构造 (Path, ParentName, RootId)。
    /// Path 含本节点（"根 / … / 父 / 本节点"）；RootId 取最顶端已知祖先
    /// （父链断裂时退化为最顶端已知节点，尽力而为）。
    /// </summary>
    public static (string Path, string? ParentName, string RootId) BuildPath(
        SpaceTree node, IReadOnlyDictionary<string, SpaceTree> nodes)
    {
        var line = new List<string> { node.Name };
        string? parentName = null;
        var current = node;

        while (current.ParentId is not null &&
               nodes.TryGetValue(current.ParentId, out var parent))
        {
            parentName ??= parent.Name;
            line.Insert(0, parent.Name);
            current = parent;
        }

        return (string.Join(" / ", line), parentName, current.Id);
    }

    /// <summary>Qdrant payload 的 snake_case 宿主类型 → 前端契约 HostType。</summary>
    private static string MapHostType(string payloadHostType) => payloadHostType switch
    {
        "space_tree" => "SpaceTree",
        "unit_tree" => "UnitTree",
        "category_tree" => "CategoryTree",
        "knowledge_tree" => "KnowledgeTree",
        "line_node" => "LineNode",
        "line" => "Line",
        "tree_node" => "TreeNode",
        "entity" => "Entity",
        "row" => "Row",
        var other => other
    };
}

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
/// 路径反查仅覆盖 SpaceTree 宿主（当前向量管道只为 space_tree 构造源文本，
/// 见 <see cref="SourceTextBuilder"/>）；其余命中（tree_node / 孤儿向量）
/// 只回填 HostType 与 Score，NodeName 为 null 由调用方降级显示。
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
        CancellationToken ct = default)
    {
        var vector = await _embedding.EmbedAsync(query, ct);

        // 绝对下限（请求值优先，钳制到 [0,1]）；候选放大后由 Qdrant 原生按此过滤
        var absoluteFloor = Math.Clamp(minScore ?? _options.MinScore, 0f, 1f);
        var candidateLimit = Math.Clamp(
            topK * CandidateMultiplier, MinCandidates, MaxCandidates);
        var hits = await _store.SearchAsync(
            vector, candidateLimit, spaceId, absoluteFloor, ct);

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
        }
        var filtered = hits
            .Where(h => h.PointKind != "entity" || !nodeHitInodeIds.Contains(h.InodeId))
            .Take(topK)
            .ToList();

        // 批量反查空间树节点（含全部祖先，BFS 逐层加载）
        var inodeIds = filtered.Select(h => h.InodeId).Distinct().ToList();
        var nodes = await LoadSpaceNodesWithAncestorsAsync(inodeIds, ct);

        return filtered.Select(h =>
        {
            var isEntity = h.PointKind == "entity";
            var entityType = isEntity && !string.IsNullOrWhiteSpace(h.EntityType)
                ? h.EntityType : null;

            if (nodes.TryGetValue(h.InodeId, out var node))
            {
                var (path, parentName, rootId) = BuildPath(node, nodes);
                return new SemanticSearchItemDto(
                    InodeId: h.InodeId,
                    HostType: "SpaceTree",
                    NodeName: node.Name,
                    ParentName: parentName,
                    Path: path,
                    RootId: rootId,
                    Score: (float)Math.Round(h.Score, 4),
                    MatchKind: isEntity ? "Entity" : "Node",
                    EntityType: entityType);
            }

            // 未挂载空间树的命中（tree_node 裸实体 / 待对账清理的孤儿向量）
            return new SemanticSearchItemDto(
                InodeId: h.InodeId,
                HostType: MapHostType(h.HostType),
                NodeName: null,
                ParentName: null,
                Path: "",
                RootId: null,
                Score: (float)Math.Round(h.Score, 4),
                MatchKind: isEntity ? "Entity" : "Node",
                EntityType: entityType);
        }).ToList();
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
        var chain = new List<string> { node.Name };
        string? parentName = null;
        var current = node;

        while (current.ParentId is not null &&
               nodes.TryGetValue(current.ParentId, out var parent))
        {
            parentName ??= parent.Name;
            chain.Insert(0, parent.Name);
            current = parent;
        }

        return (string.Join(" / ", chain), parentName, current.Id);
    }

    /// <summary>Qdrant payload 的 snake_case 宿主类型 → 前端契约 HostType。</summary>
    private static string MapHostType(string payloadHostType) => payloadHostType switch
    {
        "space_tree" => "SpaceTree",
        "tree_node" => "TreeNode",
        "entity" => "Entity",
        var other => other
    };
}

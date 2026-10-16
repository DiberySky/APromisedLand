using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Shared.NodeEavSky.Dtos;
using TreeGraph.Shared.TreeEavSky.Entities;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>
/// 语义搜索服务：query → 向量 → Qdrant → 空间树反查（名称/父名/路径/根）。
///
/// 路径反查仅覆盖 SpaceTree 宿主（当前向量管道只为 space_tree 构造源文本，
/// 见 <see cref="SourceTextBuilder"/>）；其余命中（tree_node / 孤儿向量）
/// 只回填 HostType 与 Score，NodeName 为 null 由调用方降级显示。
/// </summary>
public sealed class SearchService
{
    private readonly EmbeddingClient _embedding;
    private readonly QdrantVectorStore _store;
    private readonly TreeGraphDbContext _db;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        EmbeddingClient embedding,
        QdrantVectorStore store,
        TreeGraphDbContext db,
        ILogger<SearchService> logger)
    {
        _embedding = embedding;
        _store = store;
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SemanticSearchItemDto>> SearchAsync(
        string query, int topK, string? spaceId,
        CancellationToken ct = default)
    {
        var vector = await _embedding.EmbedAsync(query, ct);
        var hits = await _store.SearchAsync(vector, topK, spaceId, ct);

        // 批量反查空间树节点（含全部祖先，BFS 逐层加载）
        var inodeIds = hits.Select(h => h.InodeId).ToList();
        var nodes = await LoadSpaceNodesWithAncestorsAsync(inodeIds, ct);

        return hits.Select(h =>
        {
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
                    Score: (float)Math.Round(h.Score, 4));
            }

            // 未挂载空间树的命中（tree_node 裸实体 / 待对账清理的孤儿向量）
            return new SemanticSearchItemDto(
                InodeId: h.InodeId,
                HostType: MapHostType(h.HostType),
                NodeName: null,
                ParentName: null,
                Path: "",
                RootId: null,
                Score: (float)Math.Round(h.Score, 4));
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
        var other => other
    };
}

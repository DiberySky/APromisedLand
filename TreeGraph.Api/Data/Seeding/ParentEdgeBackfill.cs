using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.GraphSyncSky;

namespace TreeGraph.Api.Data.Seeding;

/// <summary>
/// 存量树节点 parent_of 派生边回填（幂等，仅补插缺失行，不删不动既有行）。
/// 背景：parent_of 落 PG 的能力上线前创建的历史树节点没有对应边，
/// 「关系绑定」页签不可见；启动时对四棵宿主树（单位/分类/知识/空间树）扫描补齐。
/// 依赖 BuiltinEdgeTypeSeeder 已播种 parent_of 边类型（先跑播种再跑本回填）。
/// </summary>
public static class ParentEdgeBackfill
{
    /// <summary>回填全部缺失的 parent_of 边，返回补插行数。</summary>
    public static async Task<int> BackfillAsync(TreeGraphDbContext db, CancellationToken ct = default)
    {
        // 已播种 parent_of 类型的空间集合
        var typeSpaces = (await db.EdgeTypes.AsNoTracking()
            .Where(t => t.Name == GraphNaming.ParentOfEdge)
            .Select(t => t.SpaceId)
            .ToListAsync(ct)).ToHashSet();
        if (typeSpaces.Count == 0) return 0;

        // 已存在的 parent_of 边（spaceId|dstVid），避免重复补插
        var existingKeys = (await db.Edges.AsNoTracking()
            .Where(e => e.EdgeType == GraphNaming.ParentOfEdge)
            .Select(e => e.SpaceId + "|" + e.DstVid)
            .ToListAsync(ct)).ToHashSet();

        // 外键守卫：父节点必须已注册 nodes
        var nodeIds = (await db.Nodes.AsNoTracking().Select(n => n.Id).ToListAsync(ct)).ToHashSet();

        var toAdd = new List<Edge>();

        void Collect(IEnumerable<(string Id, string? ParentId)> rows, string spaceId)
        {
            if (!typeSpaces.Contains(spaceId)) return;
            foreach (var (id, parentId) in rows)
            {
                if (parentId is null || !nodeIds.Contains(parentId)) continue;
                if (!existingKeys.Add(spaceId + "|" + id)) continue;
                toAdd.Add(new Edge
                {
                    SpaceId = spaceId,
                    EdgeType = GraphNaming.ParentOfEdge,
                    SrcVid = parentId,
                    DstVid = id,
                    Rank = 0
                });
            }
        }

        var units = await db.UnitTrees.AsNoTracking()
            .Select(t => new { t.Id, t.ParentId }).ToListAsync(ct);
        Collect(units.Select(t => (t.Id, t.ParentId)), SeedSpaceIds.Unit);

        var categories = await db.CategoryTrees.AsNoTracking()
            .Select(t => new { t.Id, t.ParentId }).ToListAsync(ct);
        Collect(categories.Select(t => (t.Id, t.ParentId)), SeedSpaceIds.Type);

        var knowledges = await db.KnowledgeTrees.AsNoTracking()
            .Select(t => new { t.Id, t.ParentId }).ToListAsync(ct);
        Collect(knowledges.Select(t => (t.Id, t.ParentId)), SeedSpaceIds.Knowledge);

        // 空间树：归属空间 = 根祖先（根节点 Id 即 Space.Id）
        var spaceRows = await db.SpaceTrees.AsNoTracking()
            .Select(t => new { t.Id, t.ParentId }).ToListAsync(ct);
        var parentOf = spaceRows.ToDictionary(r => r.Id, r => r.ParentId);
        string RootOf(string id)
        {
            var current = id;
            for (var depth = 0; depth < 64 && parentOf.TryGetValue(current, out var p) && p is not null; depth++)
                current = p;
            return current;
        }
        foreach (var group in spaceRows.Where(r => r.ParentId is not null).GroupBy(r => RootOf(r.Id)))
            Collect(group.Select(r => (r.Id, r.ParentId)), group.Key);

        if (toAdd.Count == 0) return 0;
        db.Edges.AddRange(toAdd);
        await db.SaveChangesAsync(ct);
        return toAdd.Count;
    }
}

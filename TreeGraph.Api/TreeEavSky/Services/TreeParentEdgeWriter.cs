using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.GraphSyncSky;

namespace TreeGraph.Api.TreeEavSky.Services;

/// <summary>
/// 树父子关系 → PG parent_of 派生边写入器。
/// 树服务写路径（创建/移动/更新）在 SaveChanges 成功后直接调用，与 Nebula 同步管道解耦：
/// EdgeSky 为独立模块（PG edges 表为唯一事实源），同步到 Nebula 仅单向传递，
/// Nebula 是否就绪不影响本写入。
/// 语义与 GraphSynchronizer 的 Nebula 对齐一致：五元组 &lt;spaceId, parent_of, srcVid, 0, dstVid&gt;，
/// 删除旧父残留行、当前父缺失时补写；仅当两端节点与内置边类型均存在时写入，避免外键失败。
/// </summary>
public static class TreeParentEdgeWriter
{
    /// <summary>对齐某节点的 parent_of 边（parentId 为 null = 仅清理残留，即移为根节点）。</summary>
    public static async Task SyncAsync(TreeGraphDbContext db, string spaceId, string nodeId,
        string? parentId, CancellationToken ct = default)
    {
        var existing = await db.Edges
            .Where(e => e.SpaceId == spaceId
                     && e.EdgeType == GraphNaming.ParentOfEdge
                     && e.DstVid == nodeId)
            .ToListAsync(ct);

        var changed = false;

        // 非当前父 / 非 rank 0 的残留行删除（覆盖节点移动前留下的旧父边）
        var stale = existing.Where(e => e.SrcVid != parentId || e.Rank != 0).ToList();
        if (stale.Count > 0)
        {
            db.Edges.RemoveRange(stale);
            changed = true;
        }

        if (parentId is not null
            && !existing.Any(e => e.SrcVid == parentId && e.Rank == 0))
        {
            var parentExists = await db.Nodes.AsNoTracking().AnyAsync(n => n.Id == parentId, ct);
            var typeExists = await db.EdgeTypes.AsNoTracking()
                .AnyAsync(t => t.SpaceId == spaceId && t.Name == GraphNaming.ParentOfEdge, ct);
            if (parentExists && typeExists)
            {
                db.Edges.Add(new Edge
                {
                    SpaceId = spaceId,
                    EdgeType = GraphNaming.ParentOfEdge,
                    SrcVid = parentId,
                    DstVid = nodeId,
                    Rank = 0
                });
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync(ct);
    }

    /// <summary>空间树节点的归属空间 = 根祖先（根节点 Id 即 Space.Id）；带深度上限防环。</summary>
    public static async Task<string> ResolveSpaceTreeSpaceIdAsync(TreeGraphDbContext db, string nodeId,
        CancellationToken ct = default)
    {
        var current = nodeId;
        for (var depth = 0; depth < 64; depth++)
        {
            var parent = await db.SpaceTrees.AsNoTracking()
                .Where(n => n.Id == current)
                .Select(n => n.ParentId)
                .FirstOrDefaultAsync(ct);
            if (parent is null) return current;
            current = parent;
        }
        return current;
    }
}

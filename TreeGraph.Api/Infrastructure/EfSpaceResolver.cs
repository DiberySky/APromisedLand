using Microsoft.EntityFrameworkCore;
using TreeGraph.GraphSync;
using TreeGraph.Persistence.Data;
using TreeGraph.Shared.Identity;

namespace TreeGraph.Api.Infrastructure;

/// <summary>
/// 基于 EF Core 的节点→空间解析实现。
/// 口径与 <c>EdgeService.ResolveNodeSpaceIdAsync</c> 一致（当前两者并存；待空间授权激活里程碑
/// 统一让 <c>EdgeService</c> 复用本实现以消除重复）：
/// 按 <c>nodes.host_type</c> 路由到种子空间映射、空间树递归 CTE 取根、lines.space_id、space_nodes.space_id。
/// </summary>
public sealed class EfSpaceResolver : ISpaceResolver
{
    private readonly TreeGraphDbContext _db;

    public EfSpaceResolver(TreeGraphDbContext db) => _db = db;

    public async Task<string?> ResolveSpaceIdAsync(string nodeId, CancellationToken ct = default)
    {
        var row = await _db.Nodes.AsNoTracking()
            .Where(n => n.Id == nodeId)
            .Select(n => new { n.HostType, n.LineId })
            .FirstOrDefaultAsync(ct);
        if (row is null)
            return null;

        return row.HostType switch
        {
            "UnitTree" => SeedSpaceIds.Unit,
            "CategoryTree" => SeedSpaceIds.Type,
            "KnowledgeTree" => SeedSpaceIds.Knowledge,
            "SpaceTree" => await ResolveSpaceTreeRootAsync(nodeId, ct),
            "Line" => await _db.Lines.AsNoTracking()
                .Where(l => l.Id == nodeId)
                .Select(l => l.SpaceId)
                .FirstOrDefaultAsync(ct),
            "LineNode" => row.LineId is null
                ? null
                : await _db.Lines.AsNoTracking()
                    .Where(l => l.Id == row.LineId)
                    .Select(l => l.SpaceId)
                    .FirstOrDefaultAsync(ct),
            "SpaceNode" => await _db.SpaceNodes.AsNoTracking()
                .Where(s => s.Id == nodeId)
                .Select(s => s.SpaceId)
                .FirstOrDefaultAsync(ct),
            _ => null
        };
    }

    /// <summary>空间树根祖先解析（根节点 Id 即 Space.Id）；递归 CTE 单次查询替代 N+1 上溯。</summary>
    private async Task<string> ResolveSpaceTreeRootAsync(string nodeId, CancellationToken ct)
    {
        var root = await _db.SpaceTrees
            .FromSqlRaw(@"
                WITH RECURSIVE tree AS (
                    SELECT id, parent_id FROM space_trees WHERE id = {0}
                    UNION ALL
                    SELECT st.id, st.parent_id FROM space_trees st
                    INNER JOIN tree t ON st.id = t.parent_id
                )
                SELECT id, parent_id FROM tree WHERE parent_id IS NULL
                LIMIT 1", nodeId)
            .Select(st => st.Id)
            .FirstOrDefaultAsync(ct);
        return root ?? nodeId;
    }
}

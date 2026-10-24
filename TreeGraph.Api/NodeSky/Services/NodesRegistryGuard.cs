using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Services;

namespace TreeGraph.Api.NodeSky.Services;

/// <summary>
/// nodes 注册表守卫（R3）：认领所有注册进 nodes 的节点类型
/// （四棵宿主树 / LineNode / EdgeInstance / Node）。
///
/// EAV 宿主校验 = nodes 有一行；Edge 起点终点校验 = nodes 行且 host_type 匹配。
/// 尚未收编进 nodes 的 Line 由 LineOwnerGuard 精确认领。
/// </summary>
public sealed class NodesRegistryGuard(TreeGraphDbContext dbContext) : INodesRegistryGuard
{
    /// <summary>空串：不参与按类型精确解析，类型判定统一走 <see cref="INodesRegistryGuard"/> 分支。</summary>
    public string NodeType => string.Empty;

    public Task<bool> NodeExistsAsync(string inodeId, CancellationToken ct = default)
        => dbContext.Nodes.AnyAsync(n => n.Id == inodeId, ct);

    public Task<bool> NodeExistsOfTypeAsync(
        string nodeType, string inodeId, CancellationToken ct = default)
        => dbContext.Nodes.AnyAsync(n => n.Id == inodeId && n.HostType == nodeType, ct);

    public Task<bool> HostTypeExistsAsync(
        string nodeType, CancellationToken ct = default)
        => dbContext.Nodes.AnyAsync(n => n.HostType == nodeType, ct);

    public async Task<Dictionary<string, string>> GetNodeNamesAsync(
        IReadOnlyCollection<string> inodeIds, CancellationToken ct = default)
    {
        var rows = await dbContext.Nodes
            .AsNoTracking()
            .Where(n => inodeIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Name, n.HostType })
            .ToListAsync(ct);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        // ① 有名字的行：直接取 nodes.name；单位树附加简称，与服务层投影 "Name 【简称】" 一致
        var named = rows.Where(r => r.Name is not null).ToList();
        if (named.Count > 0)
        {
            var namedIds = named.Select(r => r.Id).ToList();
            var abbreviations = await dbContext.UnitTrees
                .AsNoTracking()
                .Where(u => namedIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Abbreviation })
                .ToDictionaryAsync(u => u.Id, u => u.Abbreviation, StringComparer.Ordinal, ct);

            foreach (var row in named)
            {
                var abbreviation = abbreviations.GetValueOrDefault(row.Id);
                result[row.Id] = string.IsNullOrEmpty(abbreviation)
                    ? row.Name!
                    : $"{row.Name} 【{abbreviation}】";
            }
        }

        // ② 边：nodes.name 为 null，显示名投影为 "fromId→toId"（DisplayName 已并入 EAV）
        var edgeIds = rows
            .Where(r => r.HostType == "EdgeInstance" && r.Name is null)
            .Select(r => r.Id)
            .ToList();
        if (edgeIds.Count > 0)
        {
            var edges = await dbContext.EdgeInstances
                .AsNoTracking()
                .Where(e => edgeIds.Contains(e.EdgeId))
                .Select(e => new { e.EdgeId, e.FromNodeId, e.ToNodeId })
                .ToListAsync(ct);

            foreach (var edge in edges)
                result[edge.EdgeId] = $"{edge.FromNodeId}→{edge.ToNodeId}";
        }

        return result;
    }
}

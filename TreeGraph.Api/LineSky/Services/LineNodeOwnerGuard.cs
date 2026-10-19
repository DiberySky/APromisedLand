using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.LineSky.Entities;

namespace TreeGraph.Api.LineSky.Services;

/// <summary>
/// LineNode 宿主守卫：实现 <see cref="IInodeOwnerGuard"/>，
/// 使 LineNode 可作为 EdgeInstance 的起终点参与校验与显示名解析。
/// </summary>
public class LineNodeOwnerGuard(TreeGraphDbContext dbContext) : IInodeOwnerGuard
{
    public string NodeType => "LineNode";

    public async Task<bool> NodeExistsAsync(string inodeId, CancellationToken ct = default)
        => await dbContext.LineNodes.AnyAsync(n => n.Id == inodeId, ct);

    public async Task<Dictionary<string, string>> GetNodeNamesAsync(
        IReadOnlyCollection<string> inodeIds, CancellationToken ct = default)
        => await dbContext.LineNodes
            .Where(n => inodeIds.Contains(n.Id))
            .ToDictionaryAsync(n => n.Id, n => n.Name, ct);
}

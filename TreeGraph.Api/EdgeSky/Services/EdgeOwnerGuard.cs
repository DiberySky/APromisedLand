using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Services;

namespace TreeGraph.Api.EdgeSky.Services;

/// <summary>
/// Edge（边实例）宿主守卫：实现 <see cref="IInodeOwnerGuard"/>，
/// 使边实例可作为 EAV 宿主挂载实体（声明类型 / 写入属性数据前校验边真实存在），
/// 并可作为 EdgeInstance 的起终点参与校验与显示名解析。
/// </summary>
public class EdgeOwnerGuard(TreeGraphDbContext dbContext) : IInodeOwnerGuard
{
    public string NodeType => "EdgeInstance";

    public async Task<bool> NodeExistsAsync(string inodeId, CancellationToken ct = default)
        => await dbContext.EdgeInstances.AnyAsync(e => e.EdgeId == inodeId, ct);

    public async Task<Dictionary<string, string>> GetNodeNamesAsync(
        IReadOnlyCollection<string> inodeIds, CancellationToken ct = default)
        => await dbContext.EdgeInstances
            .Where(e => inodeIds.Contains(e.EdgeId))
            .ToDictionaryAsync(
                e => e.EdgeId,
                e => e.DisplayName ?? e.EdgeId, ct);
}

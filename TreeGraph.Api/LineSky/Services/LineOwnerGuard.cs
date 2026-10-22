using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.LineSky.Entities;

namespace TreeGraph.Api.LineSky.Services;

/// <summary>
/// Line 宿主守卫：实现 <see cref="IInodeOwnerGuard"/>，
/// 使 Line 可作为 EAV 宿主挂载实体（声明类型 / 写入属性数据前校验线路真实存在），
/// 并可作为 EdgeInstance 的起终点参与校验与显示名解析。
/// </summary>
public class LineOwnerGuard(TreeGraphDbContext dbContext) : IInodeOwnerGuard
{
    public string NodeType => "Line";

    public async Task<bool> NodeExistsAsync(string inodeId, CancellationToken ct = default)
        => await dbContext.Lines.AnyAsync(n => n.Id == inodeId, ct);

    public async Task<Dictionary<string, string>> GetNodeNamesAsync(
        IReadOnlyCollection<string> inodeIds, CancellationToken ct = default)
        => await dbContext.Lines
            .Where(n => inodeIds.Contains(n.Id))
            .ToDictionaryAsync(n => n.Id, n => n.Name, ct);
}

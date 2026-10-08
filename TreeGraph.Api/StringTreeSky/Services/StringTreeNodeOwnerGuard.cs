using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.NodeEavSky.Dtos;
using TreeGraph.Shared.StringTreeSky.Contracts;

namespace TreeGraph.Api.StringTreeSky.Services;

/// <summary>
/// StringTreeNode 归属守卫：该实体类型的 EAV 属性必须附属于一个真实存在的
/// StringTree 节点，entityId 必须等于 <c>string_tree_sky_nodes.id</c>。
/// 禁止用任意 GUID 凭空 PUT 出无主属性实体，也封死 iNode 路径为该类型
/// 另发随机 entityId 的分叉写入。
/// </summary>
public sealed class StringTreeNodeOwnerGuard : IEntityOwnerGuard
{
    private readonly TreeGraphDbContext _db;

    public StringTreeNodeOwnerGuard(TreeGraphDbContext db) => _db = db;

    public async Task EnsureOwnerExistsAsync(
        string entityType, string entityId, CancellationToken ct = default)
    {
        if (!string.Equals(entityType, StringTreeEntityTypes.Node, StringComparison.Ordinal))
            return;

        var exists = await _db.StringTreeSkyNodes
            .AsNoTracking()
            .AnyAsync(n => n.Id == entityId, ct);

        if (!exists)
        {
            throw new EavValidationException(new List<ValidationError>
            {
                new("id",
                    $"实体类型 {entityType} 的属性必须附属于已存在的 StringTree 节点；" +
                    $"节点 {entityId} 不存在，不能凭空生成属性实体。")
            });
        }
    }
}

using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.EdgeSky.Services;

/// <summary>
/// 边/链节点校验器：当 EdgeDefinition 声明了 FromNodeType/ToNodeType 时，
/// 校验对应节点在其宿主主表中真实存在，防止边指向不存在的节点（孤儿边）。
///
/// 遵循既有"无守卫则放行"的约定：
///   - nodeType 为空 → 不校验（该关系不限定节点类型）；
///   - nodeType 为未知类型（无对应宿主表）→ 放行（无法校验）；
///   - 当前覆盖的宿主类型：UnitTree / CategoryTree / SpaceTree。
/// </summary>
public interface IEdgeNodeValidator
{
    /// <summary>
    /// 校验 (nodeType, nodeId) 的宿主存在性。
    /// 宿主不存在时抛出 <see cref="EavValidationException"/>（由统一异常管道映射为 400）。
    /// </summary>
    Task EnsureNodeAsync(string? nodeType, string nodeId, CancellationToken ct = default);
}

public sealed class EdgeNodeValidator : IEdgeNodeValidator
{
    private readonly TreeGraphDbContext _db;

    public EdgeNodeValidator(TreeGraphDbContext db) => _db = db;

    public async Task EnsureNodeAsync(
        string? nodeType, string nodeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nodeType) || string.IsNullOrWhiteSpace(nodeId))
            return;

        var exists = nodeType switch
        {
            "UnitTree" => await _db.UnitTrees.AnyAsync(n => n.Id == nodeId, ct),
            "CategoryTree" => await _db.CategoryTrees.AnyAsync(n => n.Id == nodeId, ct),
            "SpaceTree" => await _db.SpaceTrees.AnyAsync(n => n.Id == nodeId, ct),
            _ => true // 未知节点类型：无对应宿主表，跳过校验
        };

        if (!exists)
            throw new EavValidationException(
            [
                new ValidationError(nodeType,
                    $"节点 '{nodeId}' 不存在于 {nodeType} 宿主表，无法建立该关系。")
            ]);
    }
}

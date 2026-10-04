using TreeGraph.Blazor.Shared.Trees.TreeSky.Models;
using TreeGraph.Blazor.Shared.Trees.TreeSky.Services;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Services;

/// <summary>
/// <see cref="IStringTreeActionHandler"/> 的 HTTP 实现：
/// 桥接 <see cref="DiberyTreeApiClient{StringTreeNode}"/> → /StringTreeNode/* 端点。
///
/// 写操作映射：
///   - CreateChildAsync → POST /StringTreeNode
///   - UpdateNodeAsync  → PUT  /StringTreeNode/{id}
///   - DeleteNodeAsync  → DELETE /StringTreeNode/{id}
///   - MoveNodeAsync    → POST /StringTreeNode/move
///   - SortChildrenAsync→ POST /StringTreeNode/children（按列表位置重编号 SortOrder）
/// </summary>
public class ApiStringTreeActionHandler(DiberyTreeApiClient<StringTreeNode> api)
    : IStringTreeActionHandler
{
    public async Task<StringNodeMeta?> CreateChildAsync(
        string parentId, StringNodeMeta newChild, CancellationToken ct = default)
    {
        var dto = ApiStringTreeDataSource.ToDto(newChild);
        dto.ParentId = parentId;
        dto.Value!.ParentId = parentId;

        var result = await api.CreateNodeAsync(dto, ct);
        return ApiStringTreeDataSource.ToMeta(result);
    }

    public async Task<StringNodeMeta?> UpdateNodeAsync(
        StringNodeMeta node, CancellationToken ct = default)
    {
        var dto = ApiStringTreeDataSource.ToDto(node);
        var result = await api.UpdateNodeAsync(node.Id, dto, ct);
        return ApiStringTreeDataSource.ToMeta(result);
    }

    public Task<bool> DeleteNodeAsync(
        string id, CancellationToken ct = default)
        => api.DeleteNodeAsync(id, ct);

    public Task<bool> MoveNodeAsync(
        string id, string? newParentId, CancellationToken ct = default)
        => api.MoveNodeAsync(id, newParentId, ct);

    /// <summary>
    /// 重排子节点：把 orderedChildIds 包装成父节点的 Children 列表，
    /// 后端按列表位置重编号 SortOrder（不按原 SortOrder 排序）。
    /// </summary>
    public async Task<bool> SortChildrenAsync(
        string parentId, IReadOnlyList<string> orderedChildIds,
        CancellationToken ct = default)
    {
        var parent = new TreeNodeDto<StringTreeNode>
        {
            Id = parentId,
            Children = orderedChildIds
                .Select(id => new TreeNodeDto<StringTreeNode> { Id = id })
                .ToList(),
        };

        _ = await api.UpdateChildrenAsync(parent, ct);
        return true;
    }
}

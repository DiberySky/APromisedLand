using TreeGraph.Blazor.Shared.Trees.TreeSky.Models;
using TreeGraph.Blazor.Shared.Trees.TreeSky.Services;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Services;

/// <summary>
/// <see cref="IStringTreeDataSource"/> 的 HTTP 实现：
/// 桥接 <see cref="DiberyTreeApiClient{StringTreeNode}"/> → /StringTreeNode/* 端点。
///
/// 与 <see cref="InMemoryStringTreeDataSource"/> 的区别：
///   - 数据来自 TreeGraph.Api（Postgres string_tree_nodes 表）
///   - 与宿主中的 <see cref="ITreeClientService{StringTreeNode}"/> 共享同一后端
/// </summary>
public class ApiStringTreeDataSource(DiberyTreeApiClient<StringTreeNode> api)
    : IStringTreeDataSource
{
    public async Task<IReadOnlyList<StringNodeMeta>> GetRootsAsync(
        CancellationToken ct = default)
        => (await api.GetRootNodesAsync(null, ct)).Select(ToMeta).ToList();

    public async Task<IReadOnlyList<StringNodeMeta>> GetChildrenAsync(
        string parentId, CancellationToken ct = default)
        => (await api.GetChildrenAsync(parentId, ct)).Select(ToMeta).ToList();

    /// <summary>
    /// DiberyTreeApiClient 无直接 GetById 端点；
    /// 用 <see cref="DiberyTreeApiClient{T}.GetFullTreeAsync"/> 遍历查找。
    /// 大樹场景下性能不佳，仅用于详情刷新等低频操作。
    /// </summary>
    public async Task<StringNodeMeta?> GetByIdAsync(
        string id, CancellationToken ct = default)
    {
        var root = await api.GetFullTreeAsync(null, ct);
        return FindNode(root, id) is { } dto ? ToMeta(dto) : null;
    }

    public async Task<List<string>?> GetAncestorPathAsync(
        string id, CancellationToken ct = default)
    {
        var path = await api.GetAncestorPathAsync(id, ct);
        return path?.ToList();
    }

    // ============================================================
    // 映射
    // ============================================================

    internal static StringNodeMeta ToMeta(TreeNodeDto<StringTreeNode> dto)
        => new()
        {
            Id = dto.Id,
            ParentId = dto.ParentId,
            Text = dto.Text ?? dto.Value?.Name ?? string.Empty,
            Description = dto.Value?.Description,
            Icon = dto.Icon,
            HasChildren = dto.HasChildren,
            CanHaveChildren = dto.Value?.CanHaveChildren ?? true,
            SortOrder = dto.SortOrder,
            ExtraData = dto.ExtraData is null
                ? null
                : dto.ExtraData.ToDictionary(kv => kv.Key, kv => (object?)kv.Value),
        };

    internal static TreeNodeDto<StringTreeNode> ToDto(StringNodeMeta meta)
        => new()
        {
            Id = meta.Id,
            ParentId = meta.ParentId,
            Value = new StringTreeNode
            {
                Id = meta.Id,
                Name = meta.Text,
                Description = meta.Description,
                CanHaveChildren = meta.CanHaveChildren,
                SortOrder = meta.SortOrder,
                HasChildren = meta.HasChildren,
                ParentId = meta.ParentId,
            },
            Text = meta.Text,
            Icon = meta.Icon,
            HasChildren = meta.HasChildren,
            SortOrder = meta.SortOrder,
            ExtraData = meta.ExtraData is null
                ? null
                : new Dictionary<string, object>(
                    meta.ExtraData.Where(kv => kv.Value is not null)
                        .ToDictionary(kv => kv.Key, kv => kv.Value!)),
        };

    // ============================================================
    // 辅助
    // ============================================================

    private static TreeNodeDto<StringTreeNode>? FindNode(
        TreeNodeDto<StringTreeNode>? node, string id)
    {
        if (node is null) return null;
        if (node.Id == id) return node;

        if (node.Children is null) return null;
        foreach (var child in node.Children)
        {
            var found = FindNode(child, id);
            if (found is not null) return found;
        }

        return null;
    }
}

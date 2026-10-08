using TreeGraph.Shared.TreeSky.Entities;
using TreeGraph.Shared.TreeSky.Models;

namespace TreeGraph.Blazor.Shared.TreeSky.Services;

/// <summary>
/// 泛型树组件读操作适配器：包装 TreeApiClient&lt;UnitTree&gt;，
/// 添加排序逻辑与 UI 属性（Title、SelectLeaf）。
/// </summary>
public class UnitTreeClientService(TreeApiClient<UnitTree> treeClient) : ITreeClientService<UnitTree>
{
    public string Title { get; set; } = "计量单位";
    public bool NewPageShow { get; set; }
    public bool SelectLeaf { get; set; } = true;

    public async Task<IReadOnlyList<TreeNodeDto<UnitTree>>> LoadInitialDataAsync(
        string? rootId, CancellationToken ct = default)
    {
        var items = await treeClient.GetRootNodesAsync(rootId, ct);
        return OrderNodes(items);
    }

    public async Task<IReadOnlyList<TreeNodeDto<UnitTree>>> LoadChildrenAsync(
        UnitTree? parent = null, CancellationToken ct = default)
    {
        var items = parent == null
            ? await treeClient.GetRootNodesAsync(cancellationToken: ct)
            : await treeClient.GetChildrenAsync(parent.Id, ct);
        return OrderNodes(items);
    }

    public async Task<List<string>?> GetAncestorPathFromApiAsync(
        string nodeId, CancellationToken ct = default)
    {
        var path = await treeClient.GetAncestorPathAsync(nodeId, ct);
        return path == null ? null : [.. path];
    }

    private static IReadOnlyList<TreeNodeDto<UnitTree>> OrderNodes(
        IEnumerable<TreeNodeDto<UnitTree>> items)
        => [.. items.OrderBy(i => i.Value?.SortOrder).ThenBy(i => i.Text)];
}

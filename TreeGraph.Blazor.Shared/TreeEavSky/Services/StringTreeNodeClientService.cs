using TreeGraph.Blazor.Shared.TreeEavSky.Models;
using TreeGraph.Shared.TreeEavSky.Models;

namespace TreeGraph.Blazor.Shared.TreeEavSky.Services;

/// <summary>
/// 泛型树组件读操作适配器：包装 TreeApiClient&lt;StringTreeNode&gt;，
/// 添加排序逻辑与 UI 属性（Title、SelectLeaf）。
/// </summary>
public class StringTreeNodeClientService(TreeApiClient<StringTreeNode> treeClient)
    : ITreeClientService<StringTreeNode>
{
    public string Title { get; set; } = "字符串树";
    public bool NewPageShow { get; set; }
    public bool SelectLeaf { get; set; } = false;

    public async Task<IReadOnlyList<TreeNodeDto<StringTreeNode>>> LoadInitialDataAsync(
        string? rootId, CancellationToken ct = default)
    {
        var items = await treeClient.GetRootNodesAsync(rootId, ct);
        return OrderNodes(items);
    }

    public async Task<IReadOnlyList<TreeNodeDto<StringTreeNode>>> LoadChildrenAsync(
        StringTreeNode? parent = null, CancellationToken ct = default)
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

    private static IReadOnlyList<TreeNodeDto<StringTreeNode>> OrderNodes(
        IEnumerable<TreeNodeDto<StringTreeNode>> items)
        => [.. items.OrderBy(i => i.Value?.SortOrder).ThenBy(i => i.Text)];
}

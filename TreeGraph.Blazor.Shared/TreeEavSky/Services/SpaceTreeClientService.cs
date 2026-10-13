using TreeGraph.Shared.TreeEavSky.Entities;
using TreeGraph.Shared.TreeEavSky.Models;

namespace TreeGraph.Blazor.Shared.TreeEavSky.Services;

/// <summary>
/// 泛型树组件读操作适配器：包装 TreeApiClient&lt;SpaceTree&gt;，
/// 添加排序逻辑与 UI 属性（Title、SelectLeaf）。
/// </summary>
public class SpaceTreeClientService(TreeApiClient<SpaceTree> treeClient) : ITreeClientService<SpaceTree>
{
    public string EntityType => treeClient.EntityType;
    public string Title { get; set; } = "空间";
    public bool NewPageShow { get; set; }
    public bool SelectLeaf { get; set; } = true;

    public async Task<IReadOnlyList<TreeNodeDto<SpaceTree>>> LoadInitialDataAsync(
        string? rootId, CancellationToken ct = default)
    {
        var items = await treeClient.GetRootNodesAsync(rootId, ct);
        return OrderNodes(items);
    }

    public async Task<IReadOnlyList<TreeNodeDto<SpaceTree>>> LoadChildrenAsync(
        SpaceTree? parent = null, CancellationToken ct = default)
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

    private static IReadOnlyList<TreeNodeDto<SpaceTree>> OrderNodes(
        IEnumerable<TreeNodeDto<SpaceTree>> items)
        => [.. items.OrderBy(i => i.Value?.SortOrder).ThenBy(i => i.Text)];
}

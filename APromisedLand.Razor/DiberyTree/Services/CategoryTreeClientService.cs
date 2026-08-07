using APromisedLand.MauiBlazor.DiberyTree.Interfaces;
using APromisedLand.MauiBlazor.DiberyTree.Services;
using APromisedLand.Shared.DiberyTree.Models;

namespace APromisedLand.Razor.DiberyTree.Services;

public class CategoryTreeClientService(
    DiberyTreeApiClient<CategoryTree> treeClient) : ITreeClientService<CategoryTree>
{
    public bool NewPageOpen { get; set; }

    public async Task<IReadOnlyList<TreeNodeDto<CategoryTree>>> LoadInitialDataAsync(string? rootId)
    {
        var items = await treeClient.GetRootNodesAsync(rootId);
        return OrderNodes(items);
    }

    public async Task<IReadOnlyList<TreeNodeDto<CategoryTree>>> LoadChildrenAsync(CategoryTree? parent)
    {
        var items = parent == null
            ? await treeClient.GetRootNodesAsync()
            : await treeClient.GetChildrenAsync(parent.Id);             
        return OrderNodes(items);
    }

    private static IReadOnlyList<TreeNodeDto<CategoryTree>> OrderNodes(IEnumerable<TreeNodeDto<CategoryTree>> items)
        => [.. items.OrderBy(i => i.Value?.SortOrder).ThenBy(i => i.Text)];

    public async Task<List<string>?> GetAncestorPathFromApiAsync(string nodeId)
    {
        var path = await treeClient.GetAncestorPathAsync(nodeId);
        return [.. path];
    }
}
using APromisedLand.MauiBlazor.DiberyTree.Interfaces;
using APromisedLand.MauiBlazor.DiberyTree.Services;
using APromisedLand.Shared.DiberyTree.Models;

namespace APromisedLand.Razor.DiberyTree.Services;

public class UnitTreeClientService(
    DiberyTreeApiClient<UnitTree> treeClient) : ITreeClientService<UnitTree>
{
    public bool NewPageShow { get; set; } 

    public async Task<IReadOnlyList<TreeNodeDto<UnitTree>>> LoadInitialDataAsync(string? rootId)
    {
        var items = await treeClient.GetRootNodesAsync(rootId);
        return OrderNodes(items);
    }

    public async Task<IReadOnlyList<TreeNodeDto<UnitTree>>> LoadChildrenAsync(UnitTree? parent)
    {
        var items = parent == null
            ? await treeClient.GetRootNodesAsync()
            : await treeClient.GetChildrenAsync(parent.Id);             
        return OrderNodes(items);
    }

    private static IReadOnlyList<TreeNodeDto<UnitTree>> OrderNodes(IEnumerable<TreeNodeDto<UnitTree>> items)
        => [.. items.OrderBy(i => i.Value?.SortOrder).ThenBy(i => i.Text)];

    public async Task<List<string>?> GetAncestorPathFromApiAsync(string nodeId)
    {
        var path = await treeClient.GetAncestorPathAsync(nodeId);
        return [.. path];
    }
}
using MudBlazor;
using TreeGraph.TreeSky.Models;

namespace TreeGraph.TreeSky.Components.Base;

public partial class TreeSky<TItem>
{
    // ========== 数据加载 ==========
    private async Task<List<TreeItemData<TItem>>> LoadInitialDataAsync()
    {
        try
        {
            _loading = true;

            var items = await ClientService.LoadInitialDataAsync(RootId);

            _loading = false;

            return items.Select(i => i.ToTreeItemData<TItem>()).ToList();
        }
        catch (Exception e)
        {
            Message.Details("加载初始数据失败。", e.Message);
            return [];
        }
    }

    private async Task<IReadOnlyCollection<TreeItemData<TItem>>> LoadChildrenAsync(TItem? parent)
    {
        if (parent == null)
        {
            var roots = await ClientService.LoadChildrenAsync();
            return roots.Select(i => i.ToTreeItemData<TItem>()).ToList();
        }
        else
        {
            var children = await ClientService.LoadChildrenAsync(parent);
            return children.Select(i => i.ToTreeItemData<TItem>()).ToList();
        }
    }

    // ========== 刷新 ==========
    public async Task RefreshAsync()
    {
        var rootItems = await ClientService.LoadChildrenAsync();
        _items = rootItems?.Select(x => x.ToTreeItemData<TItem>()).ToList() ?? [];
        StateHasChanged();
    }

    private async Task RefreshNodeChildrenAsync(ITreeItemData<TItem> node)
    {
        SelectedValue = null;
        _ = SelectedValueChanged.InvokeAsync(null);

        StateHasChanged();

        var children = await ClientService.LoadChildrenAsync(node.Value);

        node.Children = children.Select(c => new TreeItemData<TItem>
        {
            Value = c.Value,
            Text = c.Text,
            Icon = TreeHelper.TreeItemIcons,
            Expandable = c.HasChildren,
            Expanded = false,
            Children = c.Children?.Select(x => x.ToTreeItemData<TItem>()).ToList()
        }).ToHashSet<ITreeItemData<TItem>>();

        node.Expanded = true;
        SelectedValue = node.Value;
        _ = SelectedValueChanged.InvokeAsync(node.Value);

        StateHasChanged();
    }

    private async Task ReLoadingAsync(ITreeItemData<TItem> node)
    {
        _items = await LoadInitialDataAsync();

        await ExpandToNodeAsync(node.Value!.Id);

        StateHasChanged();
    }
}

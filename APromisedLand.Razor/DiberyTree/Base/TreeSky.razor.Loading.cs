using APromisedLand.Razor.Helper.Blazor;
using APromisedLand.Shared.DiberyTree.Interfaces;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace APromisedLand.Razor.DiberyTree.Base;

public partial class TreeSky<TItem> 
{
    // ========== 数据加载 ==========
    private async Task<List<TreeItemData<TItem>>> LoadInitialDataAsync()
    {
        try
        {
            // if (LoadInitialDataFunc == null) return [];
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
        // if (LoadServerDataFunc == null) return [];

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
        // if (LoadServerDataFunc != null)
        // {
        var rootItems = await ClientService.LoadChildrenAsync();
        _items = rootItems?.Select(x => x.ToTreeItemData<TItem>()).ToList() ?? [];
        StateHasChanged();
        // }
    }

    private async Task RefreshNodeChildrenAsync(ITreeItemData<TItem> node)
    {
        // if (node.Value == null || LoadServerDataFunc == null) return;

        SelectedValue = null;
        _ = SelectedValueChanged.InvokeAsync(null);

        StateHasChanged();

        var children = await ClientService.LoadChildrenAsync(node.Value);

        node.Children = children.Select(c => new TreeItemData<TItem>
        {
            Value = c.Value,
            Text = c.Text,
            Icon = BlazorHelper.TreeItemIcons,
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

        // SelectedValue = node.Value;
        // _ = SelectedValueChanged.InvokeAsync(SelectedValue);
    }

}
using MudBlazor;
using TreeGraph.Blazor.Shared.Models;

namespace TreeGraph.Blazor.Shared.Components.Base;

public partial class TreeSky<TItem>
{
    // ========== 数据加载 ==========
    private async Task<List<TreeItemData<TItem>>> LoadInitialDataAsync(
        CancellationToken ct = default)
    {
        try
        {
            _loading = true;

            var items = await ClientService.LoadInitialDataAsync(RootId, ct);
            ct.ThrowIfCancellationRequested();

            _loading = false;

            return items.Select(i => i.ToTreeItemData<TItem>()).ToList();
        }
        catch (OperationCanceledException)
        {
            _loading = false;
            throw;
        }
        catch (Exception e)
        {
            Message.Details("加载初始数据失败。", e.Message);
            return [];
        }
    }

    private async Task<IReadOnlyCollection<TreeItemData<TItem>>> LoadChildrenAsync(
        TItem? parent, CancellationToken ct = default)
    {
        if (parent == null)
        {
            var roots = await ClientService.LoadChildrenAsync(null, ct);
            return roots.Select(i => i.ToTreeItemData<TItem>()).ToList();
        }
        else
        {
            var children = await ClientService.LoadChildrenAsync(parent, ct);
            return children.Select(i => i.ToTreeItemData<TItem>()).ToList();
        }
    }

    // ========== 刷新 ==========
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var rootItems = await ClientService.LoadChildrenAsync(null, ct);
        ct.ThrowIfCancellationRequested();

        _items = rootItems?.Select(x => x.ToTreeItemData<TItem>()).ToList() ?? [];
        StateHasChanged();
    }

    private async Task RefreshNodeChildrenAsync(
        ITreeItemData<TItem> node, CancellationToken ct = default)
    {
        SelectedValue = null;
        _ = SelectedValueChanged.InvokeAsync(null);

        StateHasChanged();

        var children = await ClientService.LoadChildrenAsync(node.Value, ct);
        ct.ThrowIfCancellationRequested();

        node.Children = children.Select(c => new TreeItemData<TItem>
        {
            Value = c.Value,
            Text = c.Text,
            Icon = TreeHelper.TreeItemIcons,
            Expandable = c.HasChildren,
            Expanded = false,
            Children = c.Children?.Select(x => x.ToTreeItemData<TItem>()).ToList()
        }).ToList();

        node.Expanded = true;
        SelectedValue = node.Value;
        _ = SelectedValueChanged.InvokeAsync(node.Value);

        StateHasChanged();
    }

    private async Task ReLoadingAsync(
        ITreeItemData<TItem> node, CancellationToken ct = default)
    {
        _items = await LoadInitialDataAsync(ct);

        await ExpandToNodeAsync(node.Value!.Id, ct: ct);

        StateHasChanged();
    }
}

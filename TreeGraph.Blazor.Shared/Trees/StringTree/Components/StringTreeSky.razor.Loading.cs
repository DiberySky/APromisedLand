using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components;

public partial class StringTreeSky
{
    // ============================================================
    // 初始数据加载
    // ============================================================

    private async Task<List<TreeItemData<string>>> LoadInitialDataAsync(
        CancellationToken ct = default)
    {
        try
        {
            _loading = true;

            var roots = await DataSource.GetRootsAsync(ct);
            ct.ThrowIfCancellationRequested();

            _loading = false;

            return roots.Select(ToTreeItemData).ToList();
        }
        catch (OperationCanceledException)
        {
            _loading = false;
            throw;
        }
        catch (Exception e)
        {
            _loading = false;
            Message.Details("加载初始数据失败。", e.Message);
            return [];
        }
    }

    // ============================================================
    // ServerData 回调（MudTreeView 展开未预置节点时调用）
    // ============================================================

    private async Task<IReadOnlyCollection<TreeItemData<string>>> LoadChildrenAsync(
        string? parentValue)
    {
        try
        {
            var ct = _cts.Token;

            // 防御：MudTreeView 理论上不传 null，但保留兜底
            if (string.IsNullOrEmpty(parentValue))
            {
                var roots = await DataSource.GetRootsAsync(ct);
                return roots.Select(ToTreeItemData).ToList();
            }

            var children = await DataSource.GetChildrenAsync(parentValue, ct);
            return children.Select(ToTreeItemData).ToList();
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch (Exception e)
        {
            Message.Details("加载子节点失败。", e.Message);
            return [];
        }
    }

    // ============================================================
    // DTO 转换（同时写入 meta 缓存）
    // ============================================================

    private TreeItemData<string> ToTreeItemData(StringNodeMeta meta)
    {
        // ★ 关键：Value 是 GUID，Text/Icon 挂到 TreeItemData 上（不靠 _metaCache 渲染）
        _metaCache[meta.Id] = meta;

        return new TreeItemData<string>
        {
            Value = meta.Id,
            Text = meta.Text,
            Icon = meta.Icon,
            Expandable = meta.HasChildren,
            Expanded = false,
            Selected = false,
        };
    }

    // ============================================================
    // 刷新
    // ============================================================

    /// <summary>重新加载根节点（会清空选中）。</summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        _items = await LoadInitialDataAsync(ct);

        SelectedValue = null;
        _ = SelectedValueChanged.InvokeAsync(null);
        StateHasChanged();
    }

    /// <summary>刷新指定节点的子节点。</summary>
    private async Task RefreshNodeChildrenAsync(
        ITreeItemData<string> node,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(node.Value)) return;

        SelectedValue = null;
        _ = SelectedValueChanged.InvokeAsync(null);
        StateHasChanged();

        var children = await DataSource.GetChildrenAsync(node.Value, ct);
        ct.ThrowIfCancellationRequested();

        node.Children = children
            .Select(ToTreeItemData)
            .ToList<ITreeItemData<string>>();

        node.Expanded = true;
        SelectedValue = node.Value;
        _ = SelectedValueChanged.InvokeAsync(node.Value);

        StateHasChanged();
    }
}

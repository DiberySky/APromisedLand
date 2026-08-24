using APromisedLand.Razor.DiberyTree.Trees;
using APromisedLand.Shared.DiberyTree.Interfaces;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace APromisedLand.Razor.DiberyTree.Base;

public partial class TreeSky<TItem> 
{
    // ========== 节点展开 ==========
    private async Task ExpandToNodeAsync(string targetId)
    {
        var path = _items!.GetPathToNode(targetId);

        if (path == null)
        {
            path = await GetAncestorPathFromApiAsync(targetId);
        }

        if (path != null)
        {
            SelectedValue = null;
            _ = SelectedValueChanged.InvokeAsync(null);

            StateHasChanged();

            await _items!.ExpandToNodeAsync(
                path: path,
                loadChildren: LoadChildrenAsync,
                onSelected: value =>
                {
                    SelectedValue = value;
                    _ = SelectedValueChanged.InvokeAsync(value);
                });

            StateHasChanged();
        }
    }

    private async Task<List<string>?> GetAncestorPathFromApiAsync(string nodeId)
    {
        // if (GetAncestorPathFunc == null) return null;
        return await ClientService.GetAncestorPathFromApiAsync(nodeId);
    }

    // ========== 节点点击与导航 ==========
    private async Task ClickItemText(ITreeItemData<TItem> node)
    {
        SelectedValue = node.Value;
        _ = SelectedValueChanged.InvokeAsync(node.Value);

        if (IsSelectDialog)
        {
            // 触发外部回调，由调用方决定导航行为
            await OnClickItemText.InvokeAsync(node);

            return;
        }

        if (node.Value!.Id == RootId) return;
        if (!ClientService.NewPageShow || !node.HasChildren) return;

        if (ShowDialogFunc == null)
        {
            // 记录浏览历史
            History.Push(NavigationManager.Uri, RootId, node.Value?.Id);

            if (node?.Value == null) return;
            Snackbar.Add($"点击{node.Text}", Severity.Info);
            NavigationManager.NavigateTo($"{CurrentPage}/rootId/{node.Value.Id}", forceLoad: true, replace: true);
            
            return;
        }

        // 触发外部回调，由调用方决定导航行为
        await OnClickItemText.InvokeAsync(node);
    }

    // private void ClickBack()
    // {
    //     var entry = History.Pop();
    //     if (entry == null) return;
    //
    //     var targetUrl = $"{CurrentPage}/";
    //     if (entry.RootId != null) targetUrl += $"rootId/{entry.RootId}/";
    //     if (entry.ClickNodeId != null) targetUrl += $"ClickNodeId/{entry.ClickNodeId}";
    //
    //     NavigationManager.NavigateTo(targetUrl, forceLoad: true);
    // }
    //
    // private void StartClick()
    // {
    //     History.Clear();
    //     NavigationManager.NavigateTo(SolutionService.StartPage);
    // }

    private void SetSelected()
    {
        var rootValue = FindNodeById(_items!.ToHashSet(), ClickNodeId);
        SelectedValue = rootValue;
    }
    
    // ========== 节点查找 ==========
    private TItem? FindNodeById(IEnumerable<ITreeItemData<TItem>> items, string? id)
    {
        if (id == null) return null;

        foreach (var item in items)
        {
            if (item.Value?.Id == id)
                return item.Value;

            if (item.Children?.Count > 0)
            {
                var found = FindNodeById(item.Children.ToList(), id);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    /// <summary>
    /// 从树中查找指定节点的父节点（泛型）
    /// </summary>
    private TItem? GetParentNode(string nodeId)
    {
        foreach (var item in _items!)
        {
            if (item.Children?.Any(c => c.Value?.Id == nodeId) == true)
                return item.Value;

            if (item.Children?.Count > 0)
            {
                var found = FindParentInChildren(item.Children, nodeId);
                if (found != null) return found;
            }
        }

        return null;
    }

    private TItem? FindParentInChildren(IEnumerable<ITreeItemData<TItem>> children, string nodeId)
    {
        foreach (var child in children)
        {
            if (child.Children?.Any(c => c.Value?.Id == nodeId) == true)
                return child.Value;

            if (child.Children?.Count > 0)
            {
                var found = FindParentInChildren(child.Children, nodeId);
                if (found != null) return found;
            }
        }

        return null;
    }

    // ========== 从树中移除节点 ==========
    public bool RemoveNodeFromParent(List<TreeItemData<TItem>> items, string id)
    {
        foreach (var item in items.ToList())
        {
            if (item.Value?.Id == id)
            {
                items.Remove(item);
                return true;
            }

            if (item.Children?.Count > 0)
            {
                var childList = item.Children.OfType<TreeItemData<TItem>>().ToList();
                if (RemoveNodeFromParent(childList, id))
                {
                    item.Children = childList.ToHashSet<ITreeItemData<TItem>>();
                    return true;
                }
            }
        }

        return false;
    }

    #region 获取所有节点

    /// <summary>
    /// 获取树中所有节点（扁平化）
    /// </summary>
    public async Task<List<TItem>> GetAllNodesAsync()
    {
        var result = new List<TItem>();

        if (_items == null) return result;

        foreach (var item in _items)
        {
            await CollectNodesAsync(item, result);
        }

        return result;
    }

    private async Task CollectNodesAsync(TreeItemData<TItem> node, List<TItem> result)
    {
        if (node.Value != null)
            result.Add(node.Value);

        // 如果节点未展开但有子节点，先加载
        if (node.Children?.Any() != true && node.Value?.HasChildren == true)
        {
            var children = await LoadChildrenAsync(node.Value);
            node.Children = children.ToHashSet<ITreeItemData<TItem>>();
        }

        if (node.Children?.Any() == true)
        {
            foreach (var child in node.Children.OfType<TreeItemData<TItem>>())
            {
                await CollectNodesAsync(child, result);
            }
        }
    }

    #endregion
}
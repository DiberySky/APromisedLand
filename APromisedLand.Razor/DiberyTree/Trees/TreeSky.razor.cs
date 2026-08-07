using APromisedLand.MauiBlazor.DiberyTree.Services;
using APromisedLand.Razor.Dialogs;
using APromisedLand.Razor.DiberyTree.Enums;
using APromisedLand.Razor.DiberyTree.Services;
using APromisedLand.Razor.Helper.Blazor;
using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.Services.Solution;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace APromisedLand.Razor.DiberyTree.Trees;

public partial class TreeSky<TItem> : ComponentBase
    where TItem : class, ITreeNodeBase<TItem>, new()
{
    // private string? CurrentPage;
    private List<TreeItemData<TItem>>? _items;
    private bool _isLoading = true;
    private string? _lastClickNodeId;
    private string HighlightedText { get; set; } = string.Empty;

    private TreeNodeDialogService<TItem>? _nodeDialogService;

    private TreeNodeDialogService<TItem> NodeDialogSvc =>
        _nodeDialogService ??= new TreeNodeDialogService<TItem>(DialogService);

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private DiberyTreeApiClient<TItem> TreeClient { get; set; } = null!;

    // ========== 生命周期 ==========
    protected override async Task OnInitializedAsync()
    {
        try
        {
            _isLoading = true;
            StateHasChanged();

            _items = await LoadInitialDataAsync();

            _isLoading = false;
            StateHasChanged();

            SetSelected();

            if (ShowDialogFunc == null)
            {
                // 获取当前路径的第一个段作为页面标识
                var relative = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
                CurrentPage = relative.Split('/').FirstOrDefault();
                _ = CurrentPageChanged.InvokeAsync(CurrentPage);
            }
        }
        catch (Exception e)
        {
            BlazorService.ShowError("数据加载失败。", e.Message);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(ClickNodeId) && ClickNodeId != _lastClickNodeId)
        {
            _lastClickNodeId = ClickNodeId;
            await ExpandToNodeAsync(ClickNodeId);
        }

        if (RootId == null && _items != null) RootId = _items!.FirstOrDefault()?.Value?.Id;
    }

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
        if (!ClientService.NewPageOpen || !node.HasChildren) return;

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

    private void ClickBack()
    {
        var entry = History.Pop();
        if (entry == null) return;

        var targetUrl = $"{CurrentPage}/";
        if (entry.RootId != null) targetUrl += $"rootId/{entry.RootId}/";
        if (entry.ClickNodeId != null) targetUrl += $"ClickNodeId/{entry.ClickNodeId}";

        NavigationManager.NavigateTo(targetUrl, forceLoad: true);
    }

    private void StartClick()
    {
        History.Clear();
        NavigationManager.NavigateTo(SolutionService.StartPage);
    }

    private void SetSelected()
    {
        var rootValue = FindNodeById(_items!.ToHashSet(), ClickNodeId);
        SelectedValue = rootValue;
    }


    // ========== 数据加载 ==========
    private async Task<List<TreeItemData<TItem>>> LoadInitialDataAsync()
    {
        try
        {
            // if (LoadInitialDataFunc == null) return [];
            
            var items = await ClientService.LoadInitialDataAsync(RootId);
            
            return items.Select(i => i.ToTreeItemData<TItem>()).ToList();
        }
        catch (Exception e)
        {
            Snackbar.Details("加载初始数据失败。", e.Message, DialogService);
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
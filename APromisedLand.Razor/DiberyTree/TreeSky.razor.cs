using APromisedLand.MauiBlazor.DiberyTree.Services;
using APromisedLand.Razor.DiberyTree.Enums;
using APromisedLand.Razor.DiberyTree.Models;
using APromisedLand.Razor.DiberyTree.Services;
using APromisedLand.Razor.Helper;
using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.Services.Solution;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace APromisedLand.Razor.DiberyTree;

public partial class TreeSky<TItem> where TItem : class, ITreeNodeBase, new()
{
    private List<TreeItemData<TItem>>? _items;
    private bool _isLoading = true;
    private string? _lastClickNodeId;
    private string HighlightedText { get; set; } = string.Empty;

    private TreeNodeDialogService<TItem>? _nodeDialogService;
    private TreeNodeDialogService<TItem> NodeDialogSvc =>
        _nodeDialogService ??= new TreeNodeDialogService<TItem>(DialogService);


    // ========== 参数 ==========
    [Parameter] public string? RootId { get; set; }
    [Parameter] public string? ClickNodeId { get; set; }
    [Parameter] public string? Page { get; set; }
    [Parameter] public TItem? SelectedValue { get; set; }
    [Parameter] public EventCallback<TItem?> SelectedValueChanged { get; set; }
    [Parameter] public Func<Task<IReadOnlyList<TreeNodeDto<TItem>>>>? FunLoadInitialData { get; set; }
    [Parameter] public Func<TItem?, Task<IReadOnlyList<TreeNodeDto<TItem>>>>? FunLoadServerData { get; set; }
    [Parameter] public Func<string, Task<List<string>?>>? FunGetAncestorPath { get; set; }
    [Parameter] public EventCallback<ITreeItemData<TItem>?> OnClickItem { get; set; }
    [Parameter] public EventCallback<NodeTemplate<TItem>> OnNodeAction { get; set; }

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private DiberyTreeApiClient<TItem> TreeClient { get; set; } = null!;

    // ========== 生命周期 ==========
    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;
        StateHasChanged();

        _items = await LoadInitialDataAsync();

        _isLoading = false;
        StateHasChanged();

        SetSelected();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(ClickNodeId) && ClickNodeId != _lastClickNodeId)
        {
            _lastClickNodeId = ClickNodeId;
            await ExpandToNodeAsync(ClickNodeId);
        }
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
            await _items.ExpandToNodeAsync(
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
        if (FunGetAncestorPath == null) return null;
        return await FunGetAncestorPath(nodeId);
    }

    // ========== 节点点击与导航 ==========
    private async Task ClickItem(ITreeItemData<TItem> node)
    {
        // 记录浏览历史
        History.Push(NavigationManager.Uri, RootId, node.Value?.Id);
        // 触发外部回调，由调用方决定导航行为
        await OnClickItem.InvokeAsync(node);
    }

    private void ClickBack()
    {
        var entry = History.Pop();
        if (entry == null) return;

        var targetUrl = $"{Page}/";
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
        if (FunLoadInitialData == null) return [];
        var items = await FunLoadInitialData();
        return items.Select(i => i.ToTreeItemData<TItem>()).ToList();
    }

    private async Task<IReadOnlyCollection<TreeItemData<TItem>>> LoadChildrenAsync(TItem? parent)
    {
        if (FunLoadServerData == null) return [];

        if (parent == null)
        {
            var roots = await FunLoadServerData(null);
            return roots.Select(ConvertToTreeItem).ToList();
        }
        else
        {
            var children = await FunLoadServerData(parent);
            return children.Select(ConvertToTreeItem).ToList();
        }
    }

    // ========== 刷新 ==========
    public async Task RefreshAsync()
    {
        if (FunLoadServerData != null)
        {
            var rootItems = await FunLoadServerData(null);
            _items = rootItems?.Select(dto => ConvertToTreeItem(dto)).ToList() ?? [];
            StateHasChanged();
        }
    }

    public async Task RefreshNodeChildrenAsync(ITreeItemData<TItem> node)
    {
        if (node.Value == null || FunLoadServerData == null) return;

        var children = await FunLoadServerData(node.Value);
        node.Children = children.Select(c => new TreeItemData<TItem>
        {
            Value = c.Value,
            Text = c.Text,
            Icon = c.Icon,
            Expanded = false,
            Children = new HashSet<ITreeItemData<TItem>>()
        }).ToHashSet<ITreeItemData<TItem>>();

        StateHasChanged();
    }

    // ========== 数据转换 ==========
    public TreeItemData<TItem> ConvertToTreeItem(TreeNodeDto<TItem> dto)
    {
        return new TreeItemData<TItem>
        {
            Value = dto.Value,
            Text = dto.Text,
            Icon = dto.Icon,
            Expanded = dto.Expanded,
            Children = [],
        };
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
    public TItem? GetParentNode(string nodeId)
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
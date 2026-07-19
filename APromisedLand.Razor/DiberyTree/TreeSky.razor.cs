using APromisedLand.Razor.Helper;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.Services.Solution;
using MudBlazor;

namespace APromisedLand.Razor.DiberyTree;

public partial class TreeSky<TItem>
{
    private List<TreeItemData<TItem>> _items = new();
    private bool _isLoading = true;

    private string HighlightedText { get; set; } = string.Empty;
    
    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;
        StateHasChanged();

        _items = await LoadInitialDataAsync();
        
        _isLoading = false;
        StateHasChanged();
        
        SetSelected();
    }

    // protected override void OnParametersSet()
    // {
    //     // 2. 参数变化时（如从 /tree 到 /tree/rootId/xxx）
    //     if (_items.Count > 0 )
    //     {
    //         SetSelected();
    //     }
    // }
    
    // protected override void OnAfterRender(bool firstRender)
    // {
    //     if (!firstRender)
    //     {
    //         // SetSelected();
    //     }
    // }

    private void ClickItem(ITreeItemData<TItem> node)
    {
        // var rootValue = FindNodeById(_items.ToHashSet(), RootId);
        // 保存当前位置（包含当前节点ID的URL）
        History.Push(NavigationManager.Uri, RootId, node.Value?.Id);

        // 导航到详情页
        var targetUrl = $"{Page}/rootId/{node.Value!.Id}/ClickNodeId/{node.Value!.Id}";
        NavigationManager.NavigateTo(targetUrl, forceLoad: true);
    }

    private void ClickBack()
    {
        var entry = History.Pop();
        if (entry == null) return;

        var returnUrl = entry.Url;

        var targetUrl = $"{Page}/";
        if (entry.RootId != null) targetUrl += $"rootId/{entry.RootId}/";
        if (entry.ClickNodeId != null) targetUrl += $"ClickNodeId/{entry.ClickNodeId}";
        
        NavigationManager.NavigateTo(targetUrl, forceLoad: true);
    }

    public void SetSelected()
    {
        var rootValue = FindNodeById(_items.ToHashSet(), ClickNodeId);
        SelectedValue = rootValue;
    }
    
    private void OnMoreClick(MudTreeViewItem<TItem> clickNode)
    {
        
    }
    
    private async Task<List<TreeItemData<TItem>>> LoadInitialDataAsync()
    {
        if (FunLoadInitialData == null) return [];
        
        var items = await FunLoadInitialData();
        
        return items.Select(i => i.ToTreeItemData<TItem>()).ToList();
    }
    
    /// <summary>
    /// 懒加载子节点
    /// </summary>
    private async Task<IReadOnlyCollection<TreeItemData<TItem>>> LoadChildrenAsync(TItem? parent)
    {
        if (FunLoadServerData == null) return [];
            
        if (parent == null)
        {
            // 加载根节点
            var roots = await FunLoadServerData(null);
            return roots.Select(ConvertToTreeItem).ToList();
        }
        else
        {
            // 加载指定节点的子节点
            var children = await FunLoadServerData(parent);
            return children.Select(ConvertToTreeItem).ToList();
        }
    }
    
    /// <summary>
    /// 刷新树（重新加载根节点）
    /// </summary>
    public async Task RefreshAsync()
    {
        if (FunLoadServerData != null)
        {
            var rootItems = await FunLoadServerData(null);
            _items = rootItems?.Select(dto => ConvertToTreeItem(dto)).ToList() ?? [];
            StateHasChanged();
        }
    }
    
    /// <summary>
    /// 将 TreeNodeDto 转换为 TreeItemData
    /// </summary>
    public TreeItemData<TItem> ConvertToTreeItem(TreeNodeDto<TItem> dto)
    {
        return new TreeItemData<TItem>
        {
            Value = dto.Value,
            Text = dto.Text,
            Icon = dto.Icon,
            Expanded = dto.Expanded,
            // 子节点由懒加载动态填充，初始为空
            Children = [],
            // 如果 HasChildren 为 true，则显示展开图标
            // 但 Children 必须非空才能展开，所以我们设置 CanExpand = HasChildren
            // 但 TreeItemData 没有 CanExpand 属性，MudTreeView 根据 Children 是否有内容判断
            // 因此我们在加载子节点时才填充 Children
        };
    }

    private void StartClick()
    {
        History.Clear();
        NavigationManager.NavigateTo(SolutionService.StartPage);
    }
    
    private TItem? FindNodeById(IEnumerable<ITreeItemData<TItem>> items, string? id)
    {
        if (id == null) return null;
        
        foreach (var item in items)
        {
            if (item.Value?.Id == id)
                return item.Value;

            if (item.Children?.Count > 0)
            {
                var children = item.Children.ToList();
                var found = FindNodeById(item.Children.ToList(),  id);
                if (found != null)
                    return found;
            }
        }
        return null;
    }
}
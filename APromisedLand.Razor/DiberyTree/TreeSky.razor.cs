using APromisedLand.Razor.Helper;
using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace APromisedLand.Razor.DiberyTree;

public partial class TreeSky<TItem>
{
    private List<TreeItemData<TItem>> _items = new();

    private string HighlightedText { get; set; } = string.Empty;
    
    private void OnMoreClick(MudTreeViewItem<TItem> clickNode)
    {
        
    }
    
    protected override async Task OnInitializedAsync()
    {
        _items = await LoadInitialDataAsync();

        await base.OnInitializedAsync();
        
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
}
using Microsoft.AspNetCore.Components;
using MudBlazor;
using TreeGraph.TreeSky.Models;
using TreeGraph.TreeSky.Navigation;
using TreeGraph.TreeSky.Services;

namespace TreeGraph.TreeSky.Components.Base;

public partial class TreeSky<TItem>
{
    private List<TreeItemData<TItem>>? _items;
    private string? _lastClickNodeId;
    private string HighlightedText { get; set; } = string.Empty;

    private bool _loading = true;

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    // ========== 生命周期 ==========
    protected override async Task OnInitializedAsync()
    {
        try
        {
            _items = await LoadInitialDataAsync();

            // 恢复选中（深层节点会沿祖先路径懒加载展开）
            await SetSelectedAsync();

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
            Message.Details("数据加载失败。", e.Message);
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 深层书签/刷新直达：等 MudTreeView 挂载完成后再沿路径展开并选中
        if (firstRender && _pendingDeepClickNodeId is { } targetId)
        {
            _pendingDeepClickNodeId = null;
            await ExpandToNodeAsync(targetId, clearSelection: false);
        }
    }
}

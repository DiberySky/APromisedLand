using Microsoft.AspNetCore.Components;
using MudBlazor;
using TreeGraph.TreeSky.Models;
using TreeGraph.TreeSky.Navigation;
using TreeGraph.TreeSky.Services;

namespace TreeGraph.TreeSky.Components.Base;

public partial class TreeSky<TItem> : IDisposable
{
    private List<TreeItemData<TItem>>? _items;
    private string? _lastClickNodeId;
    private string HighlightedText { get; set; } = string.Empty;

    private bool _loading = true;

    /// <summary>
    /// 组件级取消令牌：组件卸载 / 页面导航时取消所有进行中的异步操作。
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ITreeActionHandler<TItem> ActionHandler { get; set; } = default!;

    // ========== 生命周期 ==========
    protected override async Task OnInitializedAsync()
    {
        try
        {
            _items = await LoadInitialDataAsync(_cts.Token);

            // 恢复选中（深层节点会沿祖先路径懒加载展开）
            await SetSelectedAsync(_cts.Token);

            if (ShowDialogFunc == null)
            {
                // 获取当前路径的第一个段作为页面标识
                var relative = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
                CurrentPage = relative.Split('/').FirstOrDefault();
                _ = CurrentPageChanged.InvokeAsync(CurrentPage);
            }
        }
        catch (OperationCanceledException)
        {
            // 组件卸载 / 导航取消，静默忽略
        }
        catch (Exception e)
        {
            Message.Details("数据加载失败。", e.Message);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_cts.IsCancellationRequested) return;

        if (!string.IsNullOrEmpty(ClickNodeId) && ClickNodeId != _lastClickNodeId)
        {
            _lastClickNodeId = ClickNodeId;
            try
            {
                await ExpandToNodeAsync(ClickNodeId, ct: _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 导航切换，静默忽略
            }
        }

        if (RootId == null && _items != null) RootId = _items!.FirstOrDefault()?.Value?.Id;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 深层书签/刷新直达：等 MudTreeView 挂载完成后再沿路径展开并选中
        if (firstRender && _pendingDeepClickNodeId is { } targetId)
        {
            _pendingDeepClickNodeId = null;
            try
            {
                await ExpandToNodeAsync(targetId, clearSelection: false, ct: _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 组件已卸载，静默忽略
            }
        }
    }

    // ========== IDisposable ==========
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}

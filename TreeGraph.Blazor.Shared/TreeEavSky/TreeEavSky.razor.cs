using Microsoft.AspNetCore.Components;
using MudBlazor;
using TreeGraph.Shared.TreeEavSky.Models;
using TreeGraph.Shared.TreeEavSky.Abstractions;
using TreeGraph.Blazor.Shared.TreeEavSky.Navigation;

namespace TreeGraph.Blazor.Shared.TreeEavSky;

public partial class TreeEavSky<TItem> : IDisposable
{
    private List<TreeNodeItem<TItem>>? _items;
    private string? _lastClickNodeId;
    private string HighlightedText { get; set; } = string.Empty;

    private bool _loading = true;

    /// <summary>
    /// 组件级取消令牌：组件卸载 / 页面导航时取消所有进行中的异步操作。
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    // ========== 生命周期 ==========
    protected override async Task OnInitializedAsync()
    {
        try
        {
            _items = await LoadInitialDataAsync(_cts.Token);

            // 恢复选中并沿祖先路径展开（必须在树首帧挂载前完成：
            // 真实浏览器首帧是 loading 占位，OnAfterRender(firstRender) 会在异步加载完成前就消费掉；
            // 提前在模型上置 Expanded，树挂载时即带展开状态，不依赖 MudTreeViewItem 的事后响应）
            await SetSelectedAsync(_cts.Token);
            await ExpandPendingAsync(_cts.Token);

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

    /// <summary>
    /// 处理由 <see cref="SetSelectedAsync"/> 登记的初始定位目标：沿祖先路径展开并选中。
    /// 在树首帧挂载前于模型层完成，避免依赖 Mounted 后的渲染时序。
    /// </summary>
    private async Task ExpandPendingAsync(CancellationToken ct = default)
    {
        if (_pendingDeepClickNodeId is not { } targetId) return;

        _pendingDeepClickNodeId = null;
        try
        {
            await ExpandToNodeAsync(targetId, clearSelection: false, ct: ct);
        }
        catch (OperationCanceledException)
        {
            // 组件已卸载，静默忽略
        }
    }

    // ========== IDisposable ==========
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}

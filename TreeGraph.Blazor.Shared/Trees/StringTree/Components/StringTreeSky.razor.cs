using Microsoft.AspNetCore.Components;
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components;

public partial class StringTreeSky : IDisposable
{
    // ============================================================
    // 内部状态
    // ============================================================

    private List<TreeItemData<string>>? _items;
    private string? _lastClickNodeId;
    private string? _pendingDeepClickNodeId;
    private bool _loading = true;

    /// <summary>
    /// string ID → 元数据 缓存。
    ///
    /// ★ T=string 无法承载显示信息（Text/Icon），所有元数据按 ID 缓存。
    ///   每次 LoadChildren / GetById 返回时更新。
    /// </summary>
    private readonly Dictionary<string, StringNodeMeta> _metaCache = new();

    private readonly CancellationTokenSource _cts = new();

    // ============================================================
    // 参数
    // ============================================================

    /// <summary>根节点 ID。null 时用 DataSource 返回的所有根。</summary>
    [Parameter] public string? RootId { get; set; }

    /// <summary>要展开并选中的目标节点 ID（用于 URL 直达深层节点）。</summary>
    [Parameter] public string? ClickNodeId { get; set; }

    /// <summary>搜索高亮文本。</summary>
    [Parameter] public string? HighlightedText { get; set; }

    /// <summary>紧凑模式。</summary>
    [Parameter] public bool Dense { get; set; } = true;

    /// <summary>是否为"选择节点"对话框场景（隐藏操作按钮）。</summary>
    [Parameter] public bool IsSelectDialog { get; set; }

    /// <summary>当前选中节点 ID（双向绑定）。</summary>
    [Parameter] public string? SelectedValue { get; set; }

    /// <summary>选中变化回调（双向绑定另一半）。</summary>
    [Parameter] public EventCallback<string?> SelectedValueChanged { get; set; }

    /// <summary>节点点击回调（参数为完整 ITreeItemData，调用方可直接取 Text/Icon）。</summary>
    [Parameter] public EventCallback<ITreeItemData<string>?> OnClickItemText { get; set; }

    /// <summary>编辑模板（创建 / 编辑节点时弹表单）。</summary>
    [Parameter] public RenderFragment<StringNodeMeta>? EditTemplate { get; set; }

    /// <summary>操作模板（节点右侧自定义操作区，可选）。</summary>
    [Parameter] public RenderFragment<StringNodeMeta>? ActionTemplate { get; set; }

    // ============================================================
    // 生命周期
    // ============================================================

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _items = await LoadInitialDataAsync(_cts.Token);
            await SetSelectedAsync(_cts.Token);
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
            catch (OperationCanceledException) { }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 深层书签直达：等 MudTreeView 挂载完成后再沿路径展开并选中
        if (firstRender && _pendingDeepClickNodeId is { } targetId)
        {
            _pendingDeepClickNodeId = null;
            try
            {
                await ExpandToNodeAsync(targetId, clearSelection: false, ct: _cts.Token);
            }
            catch (OperationCanceledException) { }
        }
    }

    // ============================================================
    // IDisposable
    // ============================================================

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}

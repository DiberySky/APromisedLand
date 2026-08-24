using APromisedLand.MauiBlazor.DiberyTree.Services;
using APromisedLand.Razor.DiberyTree.Trees;
using APromisedLand.Razor.Helper.Blazor;
using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.Services.Solution;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace APromisedLand.Razor.DiberyTree.Base;

public partial class TreeSky<TItem> 
{
    // private string? CurrentPage;
    private List<TreeItemData<TItem>>? _items;
    private string? _lastClickNodeId;
    private string HighlightedText { get; set; } = string.Empty;
    
    private bool _loading = true;

    // private TreeNodeDialogService<TItem>? _nodeDialogService;
    // private TreeNodeDialogService<TItem> NodeDialogSvc =>
    //     _nodeDialogService ??= new TreeNodeDialogService<TItem>(DialogService);

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private DiberyTreeApiClient<TItem> TreeClient { get; set; } = null!;

    // ========== 生命周期 ==========
    protected override async Task OnInitializedAsync()
    {
        try
        {
            StateHasChanged();

            _items = await LoadInitialDataAsync();

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

    
}
using APromisedLand.MauiBlazor.DiberyTree.Services;
using APromisedLand.Razor.DiberyTree.Services;
using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace APromisedLand.Razor.DiberyTree.Category;

public partial class CategoryTreeSelectDialog : ComponentBase
{
    [Parameter] public string? RootId { get; set; }
    [Parameter] public string? ClickNodeId { get; set; }

    private TreeSky<CategoryTree>? _treeSky;
    private CategoryTree? _selectedCategory;

    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private DiberyTreeApiClient<CategoryTree> TreeClient { get; set; } = null!;

    private TreeNodeDialogService<CategoryTree>? _nodeDialogService;
    private TreeNodeDialogService<CategoryTree> NodeDialogSvc =>
        _nodeDialogService ??= new TreeNodeDialogService<CategoryTree>(DialogService);
    
    #region CRUD 操作
    
    private async Task AddRootNodeAsync()
    {
        var formModel = await NodeDialogSvc.ShowCreateDialogAsync(null);
        if (formModel == null) return;

        await RefreshTreeAsync();
        ShowSuccess("根分类已创建");
    }

    #endregion
    
    #region 数据加载

    private async Task<IReadOnlyList<TreeNodeDto<CategoryTree>>> LoadInitialDataAsync()
    {
        var items = await TreeClient.GetRootNodesAsync(RootId);
        return OrderNodes(items);
    }

    private async Task<IReadOnlyList<TreeNodeDto<CategoryTree>>> LoadChildrenAsync(CategoryTree? parent)
    {
        var items = parent == null
            ? await TreeClient.GetRootNodesAsync()
            : await TreeClient.GetChildrenAsync(parent.Id);
        return OrderNodes(items);
    }

    private static IReadOnlyList<TreeNodeDto<CategoryTree>> OrderNodes(IEnumerable<TreeNodeDto<CategoryTree>> items)
        => items.OrderBy(i => i.Value?.SortOrder).ThenBy(i => i.Text).ToList();

    private async Task<List<string>?> GetAncestorPathFromApiAsync(string nodeId)
    {
        var path = await TreeClient.GetAncestorPathAsync(nodeId);
        return path.ToList();
    }

    private async Task RefreshTreeAsync()
    {
        if (_treeSky != null)
            await _treeSky.RefreshAsync();
        _selectedCategory = null;
    }

    #endregion

    #region 通用辅助方法

    private void ShowSuccess(string message) => Snackbar.Add(message, Severity.Success);

    private async Task ExecuteTreeOperationAsync(Func<Task> operation, string successMessage)
    {
        await operation();
        await RefreshTreeAsync();
        ShowSuccess(successMessage);
    }

    #endregion
}
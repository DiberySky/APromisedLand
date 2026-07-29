using APromisedLand.MauiBlazor.DiberyTree.Services;
using APromisedLand.Razor.DiberyTree.Enums;
using APromisedLand.Razor.DiberyTree.Models;
using APromisedLand.Razor.DiberyTree.Services;
using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace APromisedLand.Razor.DiberyTree.Category;

/// <summary>
/// 分类树页面
/// </summary>
public partial class CategoryTreePage : ComponentBase
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
    
    #region 事件处理

    // private void ClickItem(ITreeItemData<CategoryTree>? node)
    // {
    //     if (node?.Value == null) return;
    //     Snackbar.Add($"点击{node.Text}", Severity.Info);
    //     NavigationManager.NavigateTo($"category-tree/rootId/{node.Value.Id}", forceLoad: true, replace: true);
    // }

    // private async Task HandleNodeActionAsync(NodeTemplate<CategoryTree> nodeTemplate)
    // {
    //     if (nodeTemplate.Node.Value == null) return;
    //
    //     var parent = _treeSky?.GetParentNode(nodeTemplate.Node.Value.Id);
    //     var isLeaf = !(nodeTemplate.Node.Children?.Count > 0 || nodeTemplate.Node.Value.HasChildren);
    //
    //     var result = await NodeDialogSvc.ShowActionsDialogAsync(nodeTemplate, parent, isLeaf);
    //     if (result == null) return;
    //
    //     await ExecuteNodeActionAsync(result.Action, nodeTemplate.Node, parent);
    // }

    // private async Task ExecuteNodeActionAsync(NodeAction action, ITreeItemData<CategoryTree> node, CategoryTree? parent)
    // {
    //     switch (action)
    //     {
    //         case NodeAction.View:
    //             await NodeDialogSvc.ShowViewDialogAsync(node.Value!, parent);
    //             break;
    //         case NodeAction.AddChild:
    //             // await HandleAddChildAsync(node);
    //             break;
    //         case NodeAction.Edit:
    //             await HandleEditAsync(node);
    //             break;
    //         case NodeAction.Delete:
    //             await HandleDeleteAsync(node);
    //             break;
    //         case NodeAction.Move:
    //             await HandleMoveNodeAsync(node);
    //             break;
    //         case NodeAction.Sort:
    //             await HandleSortAsync();
    //             break;
    //     }
    // }

    #endregion

    #region CRUD 操作

    // private async Task HandleAddChildAsync(ITreeItemData<CategoryTree> node)
    // {
    //     var nodeTemplate = new NodeTemplate<CategoryTree>
    //     {
    //         Node = node,
    //         EditTemplate = editTemplate<CategoryTree>,
    //     };
    //     var nodeTemplate = new NodeTemplate<CategoryTree>
    //     {
    //         Node = node,
    //         ActionTemplate = ActionTemplate,
    //     };
    //     
    //     var formModel = await NodeDialogSvc.ShowCreateDialogAsync(nodeTemplate);
    //     if (formModel == null) return;
    //
    //     node.Expanded = true;
    //     if (_treeSky != null)
    //         await _treeSky.RefreshNodeChildrenAsync(node);
    //     ShowSuccess("创建成功");
    // }

    // private async Task HandleEditAsync(ITreeItemData<CategoryTree> node)
    // {
    //     
    //     // var formModel = await NodeDialogSvc.ShowEditDialogAsync(node.Value!);
    //     // if (formModel == null) return;
    //     //
    //     // node.Text = formModel.Name;
    //     ShowSuccess("更新成功");
    // }

    // private async Task HandleDeleteAsync(ITreeItemData<CategoryTree> node)
    // {
    //     var hasChildren = node.Children?.Count > 0 || node.Value?.HasChildren == true;
    //     var confirmed = await NodeDialogSvc.ShowDeleteDialogAsync(node.Value!, hasChildren);
    //     if (!confirmed) return;
    //
    //     await ExecuteTreeOperationAsync(
    //         () => TreeClient.DeleteNodeAsync(node.Value!.Id),
    //         "删除成功");
    // }

    private async Task AddRootNodeAsync()
    {
        var formModel = await NodeDialogSvc.ShowCreateDialogAsync(null);
        if (formModel == null) return;

        await RefreshTreeAsync();
        ShowSuccess("根分类已创建");
    }

    #endregion

    #region 移动与排序

    // private async Task HandleMoveNodeAsync(ITreeItemData<CategoryTree> node)
    // {
    //     if (node.Value == null || _treeSky == null) return;
    //
    //     var allNodes = await _treeSky.GetAllNodesAsync();
    //     var currentParent = _treeSky.GetParentNode(node.Value.Id);
    //
    //     var selectResult = await NodeDialogSvc.ShowParentSelectDialogAsync(
    //         allNodes, node.Value, currentParent, allowRoot: true);
    //
    //     if (selectResult?.IsConfirmed != true) return;
    //
    //     var newParentId = selectResult.SelectedParent?.Id;
    //     await ExecuteTreeOperationAsync(
    //         () => TreeClient.MoveNodeAsync(node.Value.Id, newParentId),
    //         $"已移动到: {selectResult.SelectedParent?.Name ?? "根节点"}");
    // }

    // private async Task HandleSortAsync()
    // {
    //     if (_treeSky == null) return;
    //
    //     var allNodes = await _treeSky.GetAllNodesAsync();
    //     if (allNodes.Count < 2)
    //     {
    //         Snackbar.Add("节点数量不足，无需排序", Severity.Info);
    //         return;
    //     }
    //
    //     var sortResult = await NodeDialogSvc.ShowSortDialogAsync(
    //         allNodes, allowHierarchyChange: true, maxDepth: 5);
    //
    //     if (sortResult?.IsConfirmed != true) return;
    //
    //     if (sortResult is { HasOrderChanges: false, HasHierarchyChanges: false })
    //     {
    //         Snackbar.Add("未做任何变更", Severity.Info);
    //         return;
    //     }
    //
    //     var updateTasks = sortResult.SortedItems.Select(item =>
    //         TreeClient.MoveNodeAsync(item.Node.Id, item.NewParentId));
    //
    //     await Task.WhenAll(updateTasks);
    //     await RefreshTreeAsync();
    //
    //     var changeDesc = sortResult.HasHierarchyChanges ? "层级和顺序" : "顺序";
    //     ShowSuccess($"{changeDesc}更新成功");
    // }

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

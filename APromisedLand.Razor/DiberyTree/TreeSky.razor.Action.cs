using APromisedLand.Razor.DiberyTree.Enums;
using APromisedLand.Razor.DiberyTree.Models;
using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.DiberyTree.Models;
using MudBlazor;

namespace APromisedLand.Razor.DiberyTree;

public partial class TreeSky<TItem> where TItem : class, ITreeNode, new()
{
    // ========== 节点操作回调（由外部页面处理具体业务） ==========
    private async Task ShowNodeActionsAsync(ITreeItemData<TItem> node)
    {
        if (node.Value == null) return;
        var nodeTemplate = new NodeTemplate<TItem>
        {
            Node = node,
            ActionTemplate = ActionTemplate,
        };
    
        // await OnNodeAction.InvokeAsync(nodeTemplate);
        
        await HandleNodeActionAsync(nodeTemplate);
    }
    
    private async Task HandleNodeActionAsync(NodeTemplate<TItem> nodeTemplate)
    {
        if (nodeTemplate.Node.Value == null) return;

        var parent = GetParentNode(nodeTemplate.Node.Value.ParentId!);
        var isLeaf = !(nodeTemplate.Node.Children?.Count > 0 || nodeTemplate.Node.Value.HasChildren);

        var result = await NodeDialogSvc.ShowActionsDialogAsync(nodeTemplate, parent, isLeaf);
        if (result == null) return;

        await ExecuteNodeActionAsync(result.Action, nodeTemplate.Node, parent);
    }

    private async Task ExecuteNodeActionAsync(NodeAction action, ITreeItemData<TItem> node, TItem? parent)
    {
        switch (action)
        {
            case NodeAction.View:
                await NodeDialogSvc.ShowViewDialogAsync(node.Value!, parent);
                break;
            case NodeAction.AddChild:
                await HandleAddChildAsync(node, parent);
                break;
            case NodeAction.Edit:
                await HandleEditAsync(node);
                break;
            case NodeAction.Delete:
                await HandleDeleteAsync(node);
                break;
            case NodeAction.Move:
                await HandleMoveNodeAsync(node);
                break;
            case NodeAction.Sort:
                await HandleSortAsync();
                break;
        }
    }

    #region CRUD 操作

    private async Task HandleAddChildAsync(ITreeItemData<TItem> node, TItem? parent)
    {
        var formModel = await NodeDialogSvc.ShowCreateDialogAsync(parent);
        if (formModel == null) return;

        node.Expanded = true;
        await RefreshNodeChildrenAsync(node);
        ShowSuccess("创建成功");
    }

    private async Task HandleEditAsync(ITreeItemData<TItem> node)
    {
        if (node.Value == null) return;
        var nodeTemplate = new NodeTemplate<TItem>
        {
            Node = node,
            EditTemplate = EditTemplate,
        };
        
        var formModel = await NodeDialogSvc.ShowEditDialogAsync(nodeTemplate, node.Value!);
        if (formModel == null) return;

        node.Text = formModel.Name;
        ShowSuccess("更新成功");
    }

    private async Task HandleDeleteAsync(ITreeItemData<TItem> node)
    {
        var hasChildren = node.Children?.Count > 0 || node.Value?.HasChildren == true;
        var confirmed = await NodeDialogSvc.ShowDeleteDialogAsync(node.Value!, hasChildren);
        if (!confirmed) return;

        await ExecuteTreeOperationAsync(
            () => TreeClient.DeleteNodeAsync(node.Value!.Id),
            "删除成功");
    }

    private async Task AddRootNodeAsync()
    {
        var formModel = await NodeDialogSvc.ShowCreateDialogAsync(null);
        if (formModel == null) return;

        await RefreshTreeAsync();
        ShowSuccess("根分类已创建");
    }

    #endregion

    #region 移动与排序

    private async Task HandleMoveNodeAsync(ITreeItemData<TItem> node)
    {
        if (node.Value == null) return;

        var allNodes = await GetAllNodesAsync();
        var currentParent = GetParentNode(node.Value.Id);

        var selectResult = await NodeDialogSvc.ShowParentSelectDialogAsync(
            allNodes, node.Value, currentParent, allowRoot: true);

        if (selectResult?.IsConfirmed != true) return;

        var newParentId = selectResult.SelectedParent?.Id;
        await ExecuteTreeOperationAsync(
            () => TreeClient.MoveNodeAsync(node.Value.Id, newParentId),
            $"已移动到: {selectResult.SelectedParent?.Text() ?? "根节点"}");
    }

    private async Task HandleSortAsync()
    {
        var allNodes = await GetAllNodesAsync();
        if (allNodes.Count < 2)
        {
            Snackbar.Add("节点数量不足，无需排序", Severity.Info);
            return;
        }

        var sortResult = await NodeDialogSvc.ShowSortDialogAsync(
            allNodes, allowHierarchyChange: true, maxDepth: 5);

        if (sortResult?.IsConfirmed != true) return;

        if (!sortResult.HasOrderChanges && !sortResult.HasHierarchyChanges)
        {
            Snackbar.Add("未做任何变更", Severity.Info);
            return;
        }

        var updateTasks = sortResult.SortedItems.Select(item =>
            TreeClient.MoveNodeAsync(item.Node.Id, item.NewParentId));

        await Task.WhenAll(updateTasks);
        await RefreshTreeAsync();

        var changeDesc = sortResult.HasHierarchyChanges ? "层级和顺序" : "顺序";
        ShowSuccess($"{changeDesc}更新成功");
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

    #region 数据加载

    // private async Task<IReadOnlyList<TreeNodeDto<TItem>>> LoadInitialDataAsync()
    // {
    //     var items = await TreeClient.GetRootNodesAsync(RootId);
    //     return OrderNodes(items);
    // }
    //
    // private async Task<IReadOnlyList<TreeNodeDto<TItem>>> LoadChildrenAsync(TItem? parent)
    // {
    //     var items = parent == null
    //         ? await TreeClient.GetRootNodesAsync()
    //         : await TreeClient.GetChildrenAsync(parent.Id);
    //     return OrderNodes(items);
    // }

    private static IReadOnlyList<TreeNodeDto<TItem>> OrderNodes(IEnumerable<TreeNodeDto<TItem>> items)
        => items.OrderBy(i => i.Value?.SortOrder).ThenBy(i => i.Text).ToList();

    // private async Task<List<string>?> GetAncestorPathFromApiAsync(string nodeId)
    // {
    //     var path = await TreeClient.GetAncestorPathAsync(nodeId);
    //     return path.ToList();
    // }

    private async Task RefreshTreeAsync()
    {
        await RefreshAsync();
        SelectedValue = null;
    }

    #endregion
}
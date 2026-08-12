using APromisedLand.Razor.DiberyTree.Enums;
using APromisedLand.Razor.DiberyTree.Models;
using APromisedLand.Razor.DiberyTree.Trees;
using APromisedLand.Razor.Helper.Blazor;
using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace APromisedLand.Razor.DiberyTree.Base;

public partial class TreeSky<TItem> : ComponentBase
    where TItem : class, ITreeNodeBase<TItem>, new()
{
    // ========== 节点操作回调（由外部页面处理具体业务） ==========
    private async Task ShowAddChildActionsAsync(ITreeItemData<TItem> node)
    {
        await ExecuteNodeActionAsync(NodeAction.AddChild, node);
    }

    private async Task ShowNodeActionsAsync(ITreeItemData<TItem> node)
    {
        if (node.Value == null) return;

        var nodeTemplate = new NodeTemplate<TItem>
        {
            Node = node,
            ActionTemplate = ActionTemplate,
        };

        await HandleNodeActionAsync(nodeTemplate);
    }

    private async Task HandleNodeActionAsync(NodeTemplate<TItem> nodeTemplate)
    {
        if (nodeTemplate.Node.Value == null) return;

        var parent = GetParentNode(nodeTemplate.Node.Value.ParentId!);
        var isBoot = nodeTemplate.Node.Value!.Id == RootId;

        var result = await NodeDialogSvc.ShowActionsDialogAsync(nodeTemplate, parent, isBoot);
        if (result == null) return;

        await ExecuteNodeActionAsync(result.Action, nodeTemplate.Node);
    }

    public async Task ExecuteNodeActionAsync(NodeAction action, ITreeItemData<TItem> node)
    {
        switch (action)
        {
            case NodeAction.View:
                await NodeDialogSvc.ShowViewDialogAsync(node.Value!);
                break;
            case NodeAction.AddChild:
                await HandleAddChildAsync(node);
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
            case NodeAction.Attribute:
                await HandleAttributeAsync(node);
                break;
            case NodeAction.Sort:
                // await HandleSortAsync(node);
                break;
        }
    }

    #region CRUD 操作

    private async Task HandleAddChildAsync(ITreeItemData<TItem> parent)
    {
        var node = new TreeItemData<TItem>
        {
            Value = new TItem(),
        };
        node.Value!.ParentId = parent.Value!.Id;
        node.Value.Parent = parent.Value;

        var nodeTemplate = new NodeTemplate<TItem>
        {
            Node = node,
            EditTemplate = EditTemplate,
        };

        var formModel = await NodeDialogSvc.ShowCreateDialogAsync(nodeTemplate);
        if (formModel == null) return;

        try
        {
            var dto = new TreeNodeDto<TItem>
            {
                Id = formModel.Id,
                Text = formModel.Text(),
                Icon = BlazorHelper.TreeItemIcons,
                ParentId = parent.Value!.Id,
                Value = formModel,
            };

            await ApiClient.CreateNodeAsync(dto);

            parent.Expanded = true;
            await RefreshNodeChildrenAsync(parent);
            BlazorService.ShowSuccess("创建成功");
        }
        catch (Exception e)
        {
            BlazorService.ShowError("创建失败", e.Message);
        }
    }

    private async Task HandleEditAsync(ITreeItemData<TItem> node)
    {
        var nodeTemplate = new NodeTemplate<TItem>
        {
            Node = node,
            EditTemplate = EditTemplate,
        };

        var formModel = await NodeDialogSvc.ShowEditDialogAsync(nodeTemplate);
        if (formModel == null) return;

        try
        {
            var dto = new TreeNodeDto<TItem>
            {
                Id = formModel.Id,
                Text = formModel.Text(),
                Icon = BlazorHelper.TreeItemIcons,
                ParentId = formModel.ParentId!,
                Value = formModel,
            };

            await ApiClient.UpdateNodeAsync(dto.Id, dto);

            node.Text = formModel.Text();
            BlazorService.ShowSuccess("更新成功");
        }
        catch (Exception e)
        {
            BlazorService.ShowError("创建失败", e.Message);
        }
    }

    private async Task HandleDeleteAsync(ITreeItemData<TItem> node)
    {
        var hasChildren = node.Children?.Count > 0 || node.Value?.HasChildren == true;

        // var confirmed = await NodeDialogSvc.ShowDeleteDialogAsync(node.Value!, hasChildren);
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除节点【{node.Value?.Text()}】吗？",
            "删除", "取消");

        if (confirmed == null || !confirmed.Value) return;

        try
        {
            await ApiClient.DeleteNodeAsync(node.Value!.Id);

            var parent = _items?.FindTreeItem(node.Value!.ParentId!);
            await RefreshNodeChildrenAsync(parent!);

            BlazorService.ShowSuccess("删除成功");
        }
        catch (Exception e)
        {
            BlazorService.ShowError("删除失败", e.Message);
        }
    }

    private async Task AddRootNodeAsync()
    {
        var formModel = await NodeDialogSvc.ShowCreateDialogAsync();
        if (formModel == null) return;

        await RefreshTreeAsync();
        BlazorService.ShowSuccess("根分类已创建");
    }

    #endregion

    #region 移动与排序

    private async Task HandleMoveNodeAsync(ITreeItemData<TItem> node)
    {
        if (node.Value == null) return;

        var selectResult = await NodeDialogSvc.ShowParentSelectDialogAsync(node);

        if (selectResult == null) return;

        var message = $"节点【{node.Text}】的上级节点：【{node.Value.Parent?.Text()}】 => 【{selectResult.Text()}】!";
        var result = await BlazorService.BoolBoxAsync(message);

        if (!result) return;

        try
        {
            node.Value.ParentId = selectResult.Id;

            var dto = new TreeNodeDto<TItem>
            {
                Id = node.Value.Id,
                Text = node.Value.Text(),
                Icon = BlazorHelper.TreeItemIcons,
                ParentId = selectResult.Id,
                Value = node.Value,
            };

            await ApiClient.UpdateNodeAsync(dto.Id, dto);

            node.Value.Parent = selectResult;

            await ReLoadingAsync(node);

            BlazorService.ShowSuccess("转移成功");
        }
        catch (Exception e)
        {
            BlazorService.ShowError("转移失败", e.Message);
        }

        // var allNodes = await GetAllNodesAsync();
        // var currentParent = GetParentNode(node.Value.Id);
        //
        // var selectResult = await NodeDialogSvc.ShowParentSelectDialogAsync(
        //     allNodes, node.Value, currentParent, allowRoot: true);
        //
        // if (selectResult?.IsConfirmed != true) return;
        //
        // var newParentId = selectResult.SelectedParent?.Id;
        // await ExecuteTreeOperationAsync(
        //     () => TreeClient.MoveNodeAsync(node.Value.Id, newParentId),
        //     $"已移动到: {selectResult.SelectedParent?.Text() ?? "根节点"}");
    }

    private async Task HandleAttributeAsync(ITreeItemData<TItem> node)
    {
        var formModel = await NodeDialogSvc.ShowNodeAttributesDialogAsync(node.Value!.Id);
    }

    // private async Task HandleSortAsync(ITreeItemData<TItem> node)
    // {
    //     var sortResult = await NodeDialogSvc.ShowSortDialogAsync(node);
    //
    //     if (sortResult == null) return;
    //
    //     var nodeDto = new TreeNodeDto<TItem>
    //     {
    //         Id = node.Value!.Id,
    //         Text = node.Value.Text(),
    //         Icon = BlazorHelper.TreeItemIcons,
    //         ParentId = node.Value.ParentId!,
    //         Value = node.Value,
    //         Children = sortResult.Select(i => new TreeNodeDto<TItem>
    //         {
    //             Id = i.Id,
    //             Text = i.Text(),
    //             Icon = BlazorHelper.TreeItemIcons,
    //             ParentId = node.Value.Id,
    //             Value = i,
    //             SortOrder = i.SortOrder,
    //         }).ToList()
    //     };
    //
    //     await ApiClient.UpdateChildrenAsync(nodeDto);
    //
    //     await RefreshNodeChildrenAsync(node);
    //
    //     BlazorService.ShowSuccess($"排序成功");
    // }

    #endregion

    #region 通用辅助方法

    // private void ShowSuccess(string message) => Snackbar.Add(message, Severity.Success);

    private async Task ExecuteTreeOperationAsync(Func<Task> operation, string successMessage)
    {
        await operation();
        await RefreshTreeAsync();
        BlazorService.ShowSuccess(successMessage);
    }

    #endregion

    #region 数据加载

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

    #region 数据删除

    /// <summary>
    /// 根据 Id 递归删除树节点
    /// </summary>
    public bool RemoveById(IEnumerable<ITreeItemData<TItem>> treeItems, string id)
    {
        foreach (var item in treeItems)
        {
            // 命中当前节点
            if (item.Value?.Id == id)
            {
                if (item is TreeItemData<TItem> concreteItem)
                {
                    _items!.Remove(concreteItem);
                }

                return true;
            }

            // 递归子节点
            if (item.Children?.Count > 0)
            {
                if (RemoveById(item.Children, id))
                {
                    // 删除后检查父节点是否还有子节点，更新 HasChildren
                    if (item.Value != null)
                    {
                        item.Value.HasChildren = item.Children.Count > 0;
                    }

                    return true;
                }
            }
        }

        return false;
    }

    #endregion
}
using MudBlazor;
using TreeGraph.TreeSky.Models;

namespace TreeGraph.TreeSky.Components.Base;

public partial class TreeSky<TItem>
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
                await HandleSortAsync(node);
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
                Icon = TreeHelper.TreeItemIcons,
                ParentId = parent.Value!.Id,
                Value = formModel,
            };

            await ApiClient.CreateNodeAsync(dto, _cts.Token);

            parent.Expanded = true;
            await RefreshNodeChildrenAsync(parent, _cts.Token);
            Message.Success("创建成功");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("创建失败", e.Message);
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
                Icon = TreeHelper.TreeItemIcons,
                ParentId = formModel.ParentId!,
                Value = formModel,
            };

            await ApiClient.UpdateNodeAsync(dto.Id, dto, _cts.Token);

            node.Text = formModel.Text();
            Message.Success("更新成功");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("更新失败", e.Message);
        }
    }

    private async Task HandleDeleteAsync(ITreeItemData<TItem> node)
    {
        var hasChildren = node.Children?.Count > 0 || node.Value?.HasChildren == true;

        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除节点【{node.Value?.Text()}】吗？",
            "删除", "取消");

        if (confirmed == null || !confirmed.Value) return;

        try
        {
            await ApiClient.DeleteNodeAsync(node.Value!.Id, _cts.Token);

            var parent = _items?.FindTreeItem(node.Value!.ParentId!);
            await RefreshNodeChildrenAsync(parent!, _cts.Token);

            Message.Success("删除成功");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("删除失败", e.Message);
        }
    }

    private async Task AddRootNodeAsync()
    {
        var formModel = await NodeDialogSvc.ShowCreateDialogAsync();
        if (formModel == null) return;

        await RefreshTreeAsync(_cts.Token);
        Message.Success("根分类已创建");
    }

    #endregion

    #region 移动与排序

    private async Task HandleMoveNodeAsync(ITreeItemData<TItem> node)
    {
        if (node.Value == null) return;

        var selectResult = await NodeDialogSvc.ShowParentSelectDialogAsync(node);

        if (selectResult == null) return;

        var message = $"节点【{node.Text}】的上级节点：【{node.Value.Parent?.Text()}】 => 【{selectResult.Text()}】!";
        var result = await Message.BoolBoxAsync(message);

        if (!result) return;

        try
        {
            node.Value.ParentId = selectResult.Id;

            var dto = new TreeNodeDto<TItem>
            {
                Id = node.Value.Id,
                Text = node.Value.Text(),
                Icon = TreeHelper.TreeItemIcons,
                ParentId = selectResult.Id,
                Value = node.Value,
            };

            await ApiClient.UpdateNodeAsync(dto.Id, dto, _cts.Token);

            node.Value.Parent = selectResult;

            await ReLoadingAsync(node, _cts.Token);

            Message.Success("转移成功");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("转移失败", e.Message);
        }
    }

    /// <summary>
    /// 节点属性：属性子系统（NodeAttributesDialog 等）未包含在本次精简移植范围内。
    /// </summary>
    private Task HandleAttributeAsync(ITreeItemData<TItem> node)
    {
        Message.Info("节点属性模块未包含在 TreeSky 精简组件库中。");
        return Task.CompletedTask;
    }

    private async Task HandleSortAsync(ITreeItemData<TItem> node)
    {
        var sortResult = await NodeDialogSvc.ShowSortDialogAsync(node);

        if (sortResult == null) return;

        var nodeDto = new TreeNodeDto<TItem>
        {
            Id = node.Value!.Id,
            Text = node.Value.Text(),
            Icon = TreeHelper.TreeItemIcons,
            ParentId = node.Value.ParentId!,
            Value = node.Value,
            Children = sortResult.Select(i => new TreeNodeDto<TItem>
            {
                Id = i.Id,
                Text = i.Text(),
                Icon = TreeHelper.TreeItemIcons,
                ParentId = node.Value.Id,
                Value = i,
                SortOrder = i.SortOrder,
            }).ToList()
        };

        try
        {
            await ApiClient.UpdateChildrenAsync(nodeDto, _cts.Token);
            await RefreshNodeChildrenAsync(node, _cts.Token);

            Message.Success($"排序成功");
        }
        catch (OperationCanceledException) { }
    }

    #endregion

    #region 通用辅助方法

    private async Task ExecuteTreeOperationAsync(
        Func<Task> operation, string successMessage, CancellationToken ct = default)
    {
        await operation();
        await RefreshTreeAsync(ct);
        Message.Success(successMessage);
    }

    #endregion

    #region 数据加载

    private static IReadOnlyList<TreeNodeDto<TItem>> OrderNodes(IEnumerable<TreeNodeDto<TItem>> items)
        => items.OrderBy(i => i.Value?.SortOrder).ThenBy(i => i.Text).ToList();

    private async Task RefreshTreeAsync(CancellationToken ct = default)
    {
        await RefreshAsync(ct);
        SelectedValue = null;
    }

    #endregion
}

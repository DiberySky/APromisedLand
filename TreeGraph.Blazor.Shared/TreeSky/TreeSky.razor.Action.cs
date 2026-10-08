using MudBlazor;
using TreeGraph.Blazor.Shared.TreeSky.Contracts;
using TreeGraph.Blazor.Shared.TreeSky.Models;

namespace TreeGraph.Blazor.Shared.TreeSky;

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
        if (_cts.IsCancellationRequested) return;

        try
        {
            var created = await ActionHandler.CreateChildAsync(
                parent.Value!, formModel, _cts.Token);
            if (created is null) return;

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
            var updated = await ActionHandler.UpdateNodeAsync(formModel, _cts.Token);
            if (updated is null) return;

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
            var ok = await ActionHandler.DeleteNodeAsync(node.Value!, _cts.Token);
            if (!ok)
            {
                Message.Warning("删除失败，节点不存在");
                return;
            }

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
            // 后端 Update 不改 ParentId，移动必须走专门的 move 路由（含防环校验）
            var ok = await ActionHandler.MoveNodeAsync(
                node.Value, selectResult, _cts.Token);
            if (!ok)
            {
                Message.Warning("移动失败，可能节点不存在或试图移动到自身子节点下");
                return;
            }

            // API 成功后再同步内存（此前先改 ParentId 在失败时会造成内存与后端不一致）
            node.Value.ParentId = selectResult.Id;
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
        // 返回列表的顺序即用户拖拽后的新顺序（后端按列表位置重新编号 SortOrder）
        var sortResult = await NodeDialogSvc.ShowSortDialogAsync(node);

        if (sortResult == null) return;

        try
        {
            var ok = await ActionHandler.SortChildrenAsync(
                node.Value!, sortResult, _cts.Token);
            if (!ok) return;

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

using MudBlazor;
using TreeGraph.Blazor.Shared.TreeEavSky.Contracts;
using TreeGraph.Shared.TreeEavSky.Models;
using TreeGraph.Shared.TreeEavSky.Abstractions;

namespace TreeGraph.Blazor.Shared.TreeEavSky;

public partial class TreeEavSky<TItem>
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

        // 对话框编排：查看/编辑/属性/排序等不改变树结构的动作执行完后，
        // 重新打开宿主「节点操作」对话框，便于继续操作同一节点；
        // 用户在宿主点取消（result == null）才结束。
        // 新增子项/删除/移动会改变树结构或使当前节点失效，执行完直接退出。
        while (true)
        {
            var parent = GetParentNode(nodeTemplate.Node.Value.ParentId!);
            var isBoot = nodeTemplate.Node.Value!.Id == RootId;

            var result = await NodeDialogSvc.ShowActionsDialogAsync(nodeTemplate, parent, isBoot);
            if (result == null) return;

            await ExecuteNodeActionAsync(result.Action, nodeTemplate.Node);

            if (result.Action is not (NodeAction.View
                or NodeAction.Edit
                or NodeAction.Attribute
                or NodeAction.Sort))
            {
                return;
            }
        }
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
        // 子项为空方可删除本项：仍有子节点时直接拒绝（后端亦会拒绝）
        var hasChildren = node.Children?.Count > 0 || node.Value?.HasChildren == true;
        if (hasChildren)
        {
            Message.Warning("该节点仍有子节点，无法删除。请先删除全部子节点。");
            return;
        }

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

            var parentId = node.Value!.ParentId;
            var parent = string.IsNullOrEmpty(parentId) ? null : _items?.FindTreeItem(parentId);
            if (parent != null)
            {
                await RefreshNodeChildrenAsync(parent, _cts.Token);
            }
            else
            {
                // 删除的是根节点（或父节点未加载）：整树刷新
                await RefreshTreeAsync(_cts.Token);
            }

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
    /// 节点属性：打开独立属性管理对话框（与节点基础字段编辑解耦），
    /// 内部桥接 NodeEavSky 动态表单实现属性值的查看/编辑/定义管理。
    /// </summary>
    private async Task HandleAttributeAsync(ITreeItemData<TItem> node)
    {
        if (node.Value == null) return;
        await NodeDialogSvc.ShowNodeAttributesDialogAsync(node.Value!.Id);
    }

    private async Task HandleSortAsync(ITreeItemData<TItem> node)
    {
        // 懒加载：节点未展开时 Children 为空，排序对话框依赖 Children 渲染，先确保已加载
        if ((node.Children?.Count ?? 0) == 0)
        {
            var loaded = await LoadChildrenAsync(node.Value, _cts.Token);
            node.Children = loaded.ToList();
        }

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

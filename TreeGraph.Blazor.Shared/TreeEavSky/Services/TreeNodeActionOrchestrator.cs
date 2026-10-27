using Microsoft.AspNetCore.Components;
using MudBlazor;
using TreeGraph.Blazor.Shared.TreeEavSky.Contracts;
using TreeGraph.Blazor.Shared.TreeEavSky.Dialogs;
using TreeGraph.Shared.TreeEavSky.Abstractions;

namespace TreeGraph.Blazor.Shared.TreeEavSky.Services;

/// <summary>宿主操作循环的最终结果，供调用方决定如何刷新（对齐 LineNodeHostOutcome）。</summary>
public enum TreeNodeHostOutcome
{
    /// <summary>用户关闭宿主且无任何变更。</summary>
    Canceled,
    /// <summary>发生过变更（建子/编辑/移动/排序成功），节点仍存在。</summary>
    Changed,
    /// <summary>节点已删除。</summary>
    Deleted
}

/// <summary>单次动作执行结果（编排循环内部流转用）。</summary>
public enum TreeNodeActionExecResult
{
    /// <summary>无变更（取消/失败/只读动作）。</summary>
    None,
    /// <summary>已变更。</summary>
    Changed,
    /// <summary>节点已删除。</summary>
    Deleted
}

/// <summary>
/// 节点操作的 UI 环境回调（树内 TreeEavSky 与搜索页等树外场景的差异全部经此注入）。
/// </summary>
/// <typeparam name="TItem">节点值的类型</typeparam>
public sealed class TreeNodeActionUi<TItem>
    where TItem : class, ITreeNodeBase<TItem>, new()
{
    /// <summary>编辑模板（创建/编辑对话框的内容区）。</summary>
    public RenderFragment<TItem>? EditTemplate { get; init; }

    /// <summary>判定节点是否为当前树的根（宿主对话框据此隐藏删除/移动/编辑/属性）。</summary>
    public required Func<TItem, bool> IsRoot { get; init; }

    /// <summary>按父 Id 取父节点值（宿主工具条显示父名）；找不到返回 null。</summary>
    public required Func<string, TItem?> GetParentNode { get; init; }

    /// <summary>按 Id 查找已加载的树节点（删除后定向刷新父节点）；找不到返回 null → 走整树刷新。</summary>
    public required Func<string?, TreeNodeItem<TItem>?> FindLoadedNode { get; init; }

    /// <summary>加载子节点（排序对话框懒加载用）。</summary>
    public required Func<TItem?, CancellationToken, Task<IReadOnlyCollection<TreeNodeItem<TItem>>>> LoadChildrenAsync { get; init; }

    /// <summary>刷新指定节点的子节点区域（建子/排序成功后）。</summary>
    public required Func<TreeNodeItem<TItem>, CancellationToken, Task> RefreshNodeChildrenAsync { get; init; }

    /// <summary>整树刷新（删除根节点或父节点未加载时）。</summary>
    public required Func<CancellationToken, Task> RefreshTreeAsync { get; init; }

    /// <summary>移动成功后重载并展开到节点。</summary>
    public required Func<TreeNodeItem<TItem>, CancellationToken, Task> ReloadAndExpandAsync { get; init; }
}

/// <summary>
/// 树节点操作编排器（由 TreeEavSky 抽取的共享实现，供树组件与搜索页等树外场景复用）：
/// 打开 TreeNodeActionsDialog 宿主 → 按钮仅返回动作 → 本循环分发执行；
/// 查看/编辑/属性/排序执行完重开宿主（连续操作），新增子项/删除/移动执行完退出。
/// 逻辑自 TreeEavSky.razor.Action.cs 原样搬迁，组件状态访问以 <see cref="TreeNodeActionUi{TItem}"/> 回调注入。
/// </summary>
/// <typeparam name="TItem">节点值的类型</typeparam>
public sealed class TreeNodeActionOrchestrator<TItem>(
    TreeNodeDialogService<TItem> nodeDialogSvc,
    ITreeActionHandler<TItem> actionHandler,
    MessageService message,
    IDialogService dialogService)
    where TItem : class, ITreeNodeBase<TItem>, new()
{
    /// <summary>
    /// 宿主操作循环：返回最终结果（Canceled / Changed / Deleted），调用方据此刷新。
    /// </summary>
    public async Task<TreeNodeHostOutcome> OperateAsync(
        NodeTemplate<TItem> nodeTemplate,
        TreeNodeActionUi<TItem> ui,
        CancellationToken ct = default)
    {
        if (nodeTemplate.Node.Value == null) return TreeNodeHostOutcome.Canceled;

        var changed = false;

        // 对话框编排：查看/编辑/属性/排序等不改变树结构的动作执行完后，
        // 重新打开宿主「节点操作」对话框，便于继续操作同一节点；
        // 用户在宿主点取消（result == null）才结束。
        // 新增子项/删除/移动会改变树结构或使当前节点失效，执行完直接退出。
        while (true)
        {
            var parent = ui.GetParentNode(nodeTemplate.Node.Value.ParentId!);
            var isBoot = ui.IsRoot(nodeTemplate.Node.Value);

            var result = await nodeDialogSvc.ShowActionsDialogAsync(nodeTemplate, parent, isBoot);
            if (result == null)
                return changed ? TreeNodeHostOutcome.Changed : TreeNodeHostOutcome.Canceled;

            var exec = await ExecuteAsync(result.Action, nodeTemplate.Node, ui, ct);
            if (exec == TreeNodeActionExecResult.Deleted)
                return TreeNodeHostOutcome.Deleted;
            if (exec == TreeNodeActionExecResult.Changed)
                changed = true;

            if (result.Action is not (NodeAction.View
                or NodeAction.Edit
                or NodeAction.Sort))
            {
                return changed ? TreeNodeHostOutcome.Changed : TreeNodeHostOutcome.Canceled;
            }
        }
    }

    /// <summary>执行单个动作（无宿主循环）。</summary>
    public async Task<TreeNodeActionExecResult> ExecuteAsync(
        NodeAction action,
        TreeNodeItem<TItem> node,
        TreeNodeActionUi<TItem> ui,
        CancellationToken ct = default)
    {
        switch (action)
        {
            case NodeAction.View:
                await nodeDialogSvc.ShowViewDialogAsync(node.Value!);
                return TreeNodeActionExecResult.None;
            case NodeAction.AddChild:
                return await HandleAddChildAsync(node, ui, ct);
            case NodeAction.Edit:
                return await HandleEditAsync(node, ui, ct);
            case NodeAction.Delete:
                return await HandleDeleteAsync(node, ui, ct);
            case NodeAction.Move:
                return await HandleMoveNodeAsync(node, ui, ct);
            case NodeAction.Sort:
                return await HandleSortAsync(node, ui, ct);
            default:
                return TreeNodeActionExecResult.None;
        }
    }

    #region CRUD 操作

    private async Task<TreeNodeActionExecResult> HandleAddChildAsync(
        TreeNodeItem<TItem> parent, TreeNodeActionUi<TItem> ui, CancellationToken ct)
    {
        var node = new TreeNodeItem<TItem>
        {
            Value = new TItem(),
        };
        node.Value!.ParentId = parent.Value!.Id;
        node.Value.Parent = parent.Value;
        // 创建子项时「允许包含子节点」默认为「允许」
        node.Value.CanHaveChildren = true;

        var nodeTemplate = new NodeTemplate<TItem>
        {
            Node = node,
            EditTemplate = ui.EditTemplate,
        };

        var formModel = await nodeDialogSvc.ShowCreateDialogAsync(nodeTemplate);
        if (formModel == null) return TreeNodeActionExecResult.None;
        if (ct.IsCancellationRequested) return TreeNodeActionExecResult.None;

        try
        {
            var created = await actionHandler.CreateChildAsync(
                parent.Value!, formModel, ct);
            if (created is null) return TreeNodeActionExecResult.None;

            parent.Expanded = true;
            await ui.RefreshNodeChildrenAsync(parent, ct);
            message.Success("创建成功");
            return TreeNodeActionExecResult.Changed;
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            message.Details("创建失败", e.Message);
        }

        return TreeNodeActionExecResult.None;
    }

    private async Task<TreeNodeActionExecResult> HandleEditAsync(
        TreeNodeItem<TItem> node, TreeNodeActionUi<TItem> ui, CancellationToken ct)
    {
        var nodeTemplate = new NodeTemplate<TItem>
        {
            Node = node,
            EditTemplate = ui.EditTemplate,
        };

        var formModel = await nodeDialogSvc.ShowEditDialogAsync(nodeTemplate);
        if (formModel == null) return TreeNodeActionExecResult.None;

        try
        {
            var updated = await actionHandler.UpdateNodeAsync(formModel, ct);
            if (updated is null) return TreeNodeActionExecResult.None;

            node.Text = formModel.Name;
            message.Success("更新成功");
            return TreeNodeActionExecResult.Changed;
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            message.Details("更新失败", e.Message);
        }

        return TreeNodeActionExecResult.None;
    }

    private async Task<TreeNodeActionExecResult> HandleDeleteAsync(
        TreeNodeItem<TItem> node, TreeNodeActionUi<TItem> ui, CancellationToken ct)
    {
        // 子项为空方可删除本项：仍有子节点时直接拒绝（后端亦会拒绝）
        var hasChildren = node.Children?.Count > 0 || node.Value?.HasChildren == true;
        if (hasChildren)
        {
            message.Warning("该节点仍有子节点，无法删除。请先删除全部子节点。");
            return TreeNodeActionExecResult.None;
        }

        var confirmed = await dialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除节点【{node.Value?.Name}】吗？",
            "删除", "取消");

        if (confirmed == null || !confirmed.Value) return TreeNodeActionExecResult.None;

        try
        {
            var ok = await actionHandler.DeleteNodeAsync(node.Value!, ct);
            if (!ok)
            {
                message.Warning("删除失败，节点不存在");
                return TreeNodeActionExecResult.None;
            }

            var parentId = node.Value!.ParentId;
            var parent = ui.FindLoadedNode(parentId);
            if (parent != null)
            {
                await ui.RefreshNodeChildrenAsync(parent, ct);
            }
            else
            {
                // 删除的是根节点（或父节点未加载）：整树刷新
                await ui.RefreshTreeAsync(ct);
            }

            message.Success("删除成功");
            return TreeNodeActionExecResult.Deleted;
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            message.Details("删除失败", e.Message);
        }

        return TreeNodeActionExecResult.None;
    }

    #endregion

    #region 移动与排序

    private async Task<TreeNodeActionExecResult> HandleMoveNodeAsync(
        TreeNodeItem<TItem> node, TreeNodeActionUi<TItem> ui, CancellationToken ct)
    {
        if (node.Value == null) return TreeNodeActionExecResult.None;

        var selectResult = await nodeDialogSvc.ShowParentSelectDialogAsync(node);

        if (selectResult == null) return TreeNodeActionExecResult.None;

        var message1 = $"节点【{node.Text}】的上级节点：【{node.Value.Parent?.Name}】 => 【{selectResult.Name}】!";
        var result = await message.BoolBoxAsync(message1);

        if (!result) return TreeNodeActionExecResult.None;

        try
        {
            // 后端 Update 不改 ParentId，移动必须走专门的 move 路由（含防环校验）
            var ok = await actionHandler.MoveNodeAsync(
                node.Value, selectResult, ct);
            if (!ok)
            {
                message.Warning("移动失败，可能节点不存在或试图移动到自身子节点下");
                return TreeNodeActionExecResult.None;
            }

            // API 成功后再同步内存（此前先改 ParentId 在失败时会造成内存与后端不一致）
            node.Value.ParentId = selectResult.Id;
            node.Value.Parent = selectResult;

            await ui.ReloadAndExpandAsync(node, ct);

            message.Success("转移成功");
            return TreeNodeActionExecResult.Changed;
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            message.Details("转移失败", e.Message);
        }

        return TreeNodeActionExecResult.None;
    }

    private async Task<TreeNodeActionExecResult> HandleSortAsync(
        TreeNodeItem<TItem> node, TreeNodeActionUi<TItem> ui, CancellationToken ct)
    {
        // 懒加载：节点未展开时 Children 为空，排序对话框依赖 Children 渲染，先确保已加载
        if ((node.Children?.Count ?? 0) == 0)
        {
            var loaded = await ui.LoadChildrenAsync(node.Value, ct);
            node.Children = loaded.ToList();
        }

        // 返回列表的顺序即用户拖拽后的新顺序（后端按列表位置重新编号 SortOrder）
        var sortResult = await nodeDialogSvc.ShowSortDialogAsync(node);

        if (sortResult == null) return TreeNodeActionExecResult.None;

        try
        {
            var ok = await actionHandler.SortChildrenAsync(
                node.Value!, sortResult, ct);
            if (!ok) return TreeNodeActionExecResult.None;

            await ui.RefreshNodeChildrenAsync(node, ct);

            message.Success($"排序成功");
            return TreeNodeActionExecResult.Changed;
        }
        catch (OperationCanceledException) { }

        return TreeNodeActionExecResult.None;
    }

    #endregion
}

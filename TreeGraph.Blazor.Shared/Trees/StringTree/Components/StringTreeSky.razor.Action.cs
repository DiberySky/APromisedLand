using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components;

public partial class StringTreeSky
{
    // ============================================================
    // 节点操作入口
    // ============================================================

    private async Task ShowNodeActionsAsync(ITreeItemData<string> node)
    {
        if (string.IsNullOrEmpty(node.Value)) return;

        var meta = GetMeta(node.Value);
        if (meta is null) return;

        var isBoot = node.Value == RootId
            || (_items is { Count: 1 } && _items[0].Value == node.Value);

        var parentMeta = string.IsNullOrEmpty(meta.ParentId)
            ? null
            : GetMeta(meta.ParentId);

        var hasChildren = node.Children is { Count: > 0 } || meta.HasChildren;

        var result = await DialogSvc.ShowActionsDialogAsync(
            meta, parentMeta, isBoot, hasChildren);
        if (result is null) return;

        await ExecuteNodeActionAsync(result.Action, node);
    }

    public async Task ExecuteNodeActionAsync(
        StringNodeAction action, ITreeItemData<string> node)
    {
        switch (action)
        {
            case StringNodeAction.View:
                await HandleViewAsync(node);
                break;
            case StringNodeAction.AddChild:
                await HandleAddChildAsync(node);
                break;
            case StringNodeAction.Edit:
                await HandleEditAsync(node);
                break;
            case StringNodeAction.Delete:
                await HandleDeleteAsync(node);
                break;
            case StringNodeAction.Move:
                await HandleMoveAsync(node);
                break;
            case StringNodeAction.Sort:
                await HandleSortAsync(node);
                break;
        }
    }

    // ============================================================
    // 查看详情
    // ============================================================

    private async Task HandleViewAsync(ITreeItemData<string> node)
    {
        if (string.IsNullOrEmpty(node.Value)) return;
        var meta = GetMeta(node.Value);
        if (meta is null) return;

        var parentMeta = string.IsNullOrEmpty(meta.ParentId)
            ? null
            : GetMeta(meta.ParentId);

        await DialogSvc.ShowViewDialogAsync(meta, parentMeta);
    }

    // ============================================================
    // 创建子项
    // ============================================================

    private async Task HandleAddChildAsync(ITreeItemData<string> parent)
    {
        if (string.IsNullOrEmpty(parent.Value)) return;
        var parentMeta = GetMeta(parent.Value);
        if (parentMeta is null) return;

        var formModel = await DialogSvc.ShowCreateDialogAsync(
            parentMeta, EditTemplate);
        if (formModel is null) return;
        if (_cts.IsCancellationRequested) return;

        try
        {
            var created = await ActionHandler.CreateChildAsync(
                parent.Value, formModel, _cts.Token);
            if (created is null)
            {
                Message.Warning("创建失败");
                return;
            }

            _metaCache[created.Id] = created;

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

    // ============================================================
    // 编辑
    // ============================================================

    private async Task HandleEditAsync(ITreeItemData<string> node)
    {
        if (string.IsNullOrEmpty(node.Value)) return;
        var meta = GetMeta(node.Value);
        if (meta is null) return;

        var formModel = await DialogSvc.ShowEditDialogAsync(meta, EditTemplate);
        if (formModel is null) return;
        if (_cts.IsCancellationRequested) return;

        try
        {
            var updated = await ActionHandler.UpdateNodeAsync(formModel, _cts.Token);
            if (updated is null)
            {
                Message.Warning("更新失败");
                return;
            }

            _metaCache[updated.Id] = updated;
            node.Text = updated.Text;   // 立即反映到 UI
            Message.Success("更新成功");
            StateHasChanged();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("更新失败", e.Message);
        }
    }

    // ============================================================
    // 删除
    // ============================================================

    private async Task HandleDeleteAsync(ITreeItemData<string> node)
    {
        if (string.IsNullOrEmpty(node.Value)) return;
        var meta = GetMeta(node.Value);
        if (meta is null) return;

        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除节点【{meta.Text}】及其所有子节点吗？",
            yesText: "删除",
            cancelText: "取消");

        if (confirmed != true) return;
        if (_cts.IsCancellationRequested) return;

        try
        {
            var ok = await ActionHandler.DeleteNodeAsync(node.Value, _cts.Token);
            if (!ok)
            {
                Message.Warning("删除失败，节点不存在");
                return;
            }

            _metaCache.Remove(node.Value);

            if (_items is not null)
                RemoveNodeFromParent(_items, node.Value);

            Message.Success("删除成功");
            StateHasChanged();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("删除失败", e.Message);
        }
    }

    // ============================================================
    // 移动
    // ============================================================

    private async Task HandleMoveAsync(ITreeItemData<string> node)
    {
        if (string.IsNullOrEmpty(node.Value)) return;
        var meta = GetMeta(node.Value);
        if (meta is null) return;

        // 加载所有节点（可能触发懒加载）
        var allNodes = await EnsureAllNodesLoadedAsync(_cts.Token);
        if (allNodes.Count == 0) return;

        var parentMeta = string.IsNullOrEmpty(meta.ParentId)
            ? null
            : GetMeta(meta.ParentId);

        var result = await DialogSvc.ShowParentSelectDialogAsync(
            allNodes, currentNode: meta, currentParent: parentMeta);
        if (result is null || !result.IsConfirmed) return;

        var newParentId = result.SelectedParent?.Id;
        if (newParentId == meta.ParentId)
        {
            Message.Info("父节点未变化");
            return;
        }

        var confirmed = await Message.BoolBoxAsync(
            $"确定将【{meta.Text}】移动到" +
            $"【{result.SelectedParent?.Text ?? "根节点"}】下吗？");
        if (!confirmed) return;

        try
        {
            var ok = await ActionHandler.MoveNodeAsync(
                node.Value, newParentId, _cts.Token);
            if (!ok)
            {
                Message.Warning("移动失败，可能构成环或节点不存在");
                return;
            }

            // API 成功后再同步内存
            meta.ParentId = newParentId;
            await RefreshAsync(_cts.Token);
            Message.Success("移动成功");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("移动失败", e.Message);
        }
    }

    // ============================================================
    // 排序
    // ============================================================

    private async Task HandleSortAsync(ITreeItemData<string> node)
    {
        if (string.IsNullOrEmpty(node.Value)) return;

        var childrenMeta = node.Children?
            .OfType<TreeItemData<string>>()
            .Select(c => string.IsNullOrEmpty(c.Value) ? null : GetMeta(c.Value))
            .Where(m => m is not null)
            .Cast<StringNodeMeta>()
            .ToList() ?? new List<StringNodeMeta>();

        if (childrenMeta.Count < 2)
        {
            Message.Info("至少需要 2 个子节点才能排序");
            return;
        }

        var orderedIds = await DialogSvc.ShowSortDialogAsync(childrenMeta);
        if (orderedIds is null) return;

        try
        {
            var ok = await ActionHandler.SortChildrenAsync(
                node.Value, orderedIds, _cts.Token);
            if (!ok) return;

            await RefreshNodeChildrenAsync(node, _cts.Token);
            Message.Success("排序成功");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Message.Details("排序失败", e.Message);
        }
    }
}


using MudBlazor;
using TreeGraph.Blazor.Shared.TreeEavSky.Contracts;
using TreeGraph.Shared.StringTreeSky.Contracts;

namespace TreeGraph.Blazor.Shared.StringTreeSky;

/// <summary>
/// 节点操作（克隆 TreeSky.razor.Action 的操作全集与分发结构）：
/// MoreHoriz → StringNodeActionsDialog → NodeAction 分发
/// （查看详情 / 创建子项 / 设置修改 / 删除 / 移动 / 子项排序 / 属性）。
/// </summary>
public partial class StringTreeSky
{
    // ========== 创建根节点（对话框先行，对齐 TreeSky.AddRootNodeAsync） ==========

    private async Task CreateRootAsync()
    {
        var name = await ShowNameDialogAsync("创建节点", "新建节点");
        if (name is null) return;

        // 有空间时，新节点挂在当前空间下
        var dto = new StringNodeDto
        {
            Name = name,
            ParentId = string.IsNullOrEmpty(RootNodeId) ? null : RootNodeId
        };
        await Client.CreateNodeAsync(dto);
        await ReloadAsync();
        await NotifyChangedAsync();
    }

    // ========== 节点操作入口与分发（对齐 TreeSky.ShowNodeActionsAsync / ExecuteNodeActionAsync） ==========

    private async Task ShowNodeActionsAsync(ITreeItemData<string> item)
    {
        var id = item.Value!;
        var node = RequireNode(id);

        var parameters = new DialogParameters
        {
            { nameof(StringNodeActionsDialog.NodeId), id },
            { nameof(StringNodeActionsDialog.NodeName), node.Name },
            { nameof(StringNodeActionsDialog.Description), node.Description },
            { nameof(StringNodeActionsDialog.ParentName), GetParentName(node) },
            { nameof(StringNodeActionsDialog.IsRoot), node.ParentId is null },
            { nameof(StringNodeActionsDialog.HasChildren), node.HasChildren },
            { nameof(StringNodeActionsDialog.AllowCreate), Options.AllowCreate },
            { nameof(StringNodeActionsDialog.AllowRename), Options.AllowRename },
            { nameof(StringNodeActionsDialog.AllowSort), Options.AllowSort },
            { nameof(StringNodeActionsDialog.AllowDelete), Options.AllowDelete },
            { nameof(StringNodeActionsDialog.IsFiltered), _isFiltered }
        };

        var dialog = await DialogService.ShowAsync<StringNodeActionsDialog>(
            "节点操作", parameters, new DialogOptions { MaxWidth = MaxWidth.Small });
        var result = await dialog.Result;

        if (result is not { Canceled: false } || result.Data is not NodeAction action) return;

        var treeItem = FindItem(id);
        if (treeItem is null) return;

        await ExecuteNodeActionAsync(action, treeItem);
    }

    private async Task ExecuteNodeActionAsync(NodeAction action, TreeItemData<string> item)
    {
        switch (action)
        {
            case NodeAction.View:
                await HandleViewAsync(item);
                break;
            case NodeAction.AddChild:
                await HandleAddChildAsync(item);
                break;
            case NodeAction.Edit:
                await HandleEditAsync(item);
                break;
            case NodeAction.Delete:
                await HandleDeleteAsync(item);
                break;
            case NodeAction.Move:
                await HandleMoveNodeAsync(item);
                break;
            case NodeAction.Sort:
                await HandleSortAsync(item);
                break;
            case NodeAction.Attribute:
                await OpenPropertiesAsync(item.Value!);
                break;
        }
    }

    private string? GetParentName(StringNodeDto node)
        => node.ParentId is not null && _nodes.TryGetValue(node.ParentId, out var parent)
            ? parent.Name
            : null;

    // ========== 查看详情（克隆 TreeSky.HandleAttributeAsync 旁的 View 分支） ==========

    private async Task HandleViewAsync(TreeItemData<string> item)
    {
        var node = RequireNode(item.Value!);

        var parameters = new DialogParameters
        {
            { nameof(StringNodeViewDialog.Node), node },
            { nameof(StringNodeViewDialog.ParentName), GetParentName(node) ?? "根分类" }
        };

        await DialogService.ShowAsync<StringNodeViewDialog>(
            "节点详情", parameters, new DialogOptions { MaxWidth = MaxWidth.Small });
    }

    // ========== 创建子项（克隆 TreeSky.HandleAddChildAsync：对话框先行） ==========

    private async Task HandleAddChildAsync(TreeItemData<string> parent)
    {
        var parentId = parent.Value!;
        var name = await ShowNameDialogAsync("创建子项", "新建子节点");
        if (name is null) return;

        var dto = new StringNodeDto { Name = name, ParentId = parentId };
        await Client.CreateNodeAsync(dto);

        if (_nodes.TryGetValue(parentId, out var meta))
        {
            meta.HasChildren = true;
        }
        parent.Expandable = true;

        parent.Children = (await LoadServerData(parentId)).ToList();
        parent.Expanded = true;

        await NotifyChangedAsync();
    }

    // ========== 设置修改（重命名，对齐 TreeSky.HandleEditAsync） ==========

    private async Task HandleEditAsync(TreeItemData<string> item)
    {
        var id = item.Value!;
        var node = RequireNode(id);

        var name = await ShowNameDialogAsync("设置修改", node.Name);
        if (name is null || name == node.Name) return;

        node.Name = name;
        await Client.UpdateNodeAsync(node);

        item.Text = name;

        await NotifyChangedAsync();
    }

    private async Task<string?> ShowNameDialogAsync(string title, string initialName)
    {
        var parameters = new DialogParameters
        {
            { nameof(StringNodeNameDialog.Title), title },
            { nameof(StringNodeNameDialog.Name), initialName }
        };

        var dialog = await DialogService.ShowAsync<StringNodeNameDialog>(title, parameters);
        var result = await dialog.Result;

        return result is { Canceled: false } && result.Data is string name
            && !string.IsNullOrWhiteSpace(name)
            ? name
            : null;
    }

    // ========== 删除（克隆 TreeSky.HandleDeleteAsync：MessageBox 确认） ==========

    private async Task HandleDeleteAsync(TreeItemData<string> item)
    {
        var id = item.Value!;
        var node = RequireNode(id);

        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除节点【{node.Name}】吗？",
            "删除", "取消");
        if (confirmed != true) return;

        // 先取父 Id（元数据随后被移除）
        var parentId = node.ParentId;

        await Client.DeleteNodeAsync(id);

        _summaries.Remove(id);
        _nodes.Remove(id);
        SummaryService.Invalidate(EntityType, id);

        // 子树随节点一并摘除；父节点无剩余子级时收回展开箭头
        RemoveFromTree(id);

        if (parentId is not null && FindItem(parentId) is { } parent)
        {
            var hasChildrenLeft = parent.Children is { Count: > 0 };
            parent.Expandable = hasChildrenLeft;
            if (_nodes.TryGetValue(parentId, out var meta))
                meta.HasChildren = hasChildrenLeft;
        }

        if (_isFiltered)
        {
            _visibleIds?.Remove(id);
            _matchedIds?.Remove(id);
        }

        await NotifyChangedAsync();
    }

    private StringNodeDto? RequireNodeOrNull(string id)
        => _nodes.TryGetValue(id, out var node) ? node : null;

    /// <summary>从树中摘除节点（含整棵子树）。</summary>
    private bool RemoveFromTree(string id)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i].Value == id)
            {
                _items.RemoveAt(i);
                return true;
            }
            if (RemoveChild(_items[i], id)) return true;
        }
        return false;
    }

    private bool RemoveChild(TreeItemData<string> parent, string id)
    {
        if (parent.Children is not { Count: > 0 }) return false;

        var children = parent.Children.OfType<TreeItemData<string>>().ToList();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child.Value == id)
            {
                children.RemoveAt(i);
                parent.Children = children;
                return true;
            }
            if (RemoveChild(child, id)) return true;
        }
        return false;
    }

    // ========== 移动（克隆 TreeSky.HandleMoveNodeAsync：父节点选择 + 确认 + move 路由） ==========

    private async Task HandleMoveNodeAsync(TreeItemData<string> item)
    {
        var id = item.Value!;
        var node = RequireNode(id);

        var parameters = new DialogParameters
        {
            { nameof(StringNodeParentSelectDialog.Nodes), _nodes.Values.ToList() },
            { nameof(StringNodeParentSelectDialog.CurrentNodeId), id },
            { nameof(StringNodeParentSelectDialog.CurrentParentId), node.ParentId }
        };

        var dialog = await DialogService.ShowAsync<StringNodeParentSelectDialog>(
            "选择父节点", parameters, new DialogOptions { MaxWidth = MaxWidth.Medium, CloseButton = true });
        var result = await dialog.Result;

        if (result is not { Canceled: false }) return;

        // Data 为选中的父节点 Id；null 表示移动到根
        var newParentId = result.Data as string;
        if (newParentId == node.ParentId) return;

        var newParentName = newParentId is not null && _nodes.TryGetValue(newParentId, out var np)
            ? np.Name : "根节点";
        var oldParentName = GetParentName(node) ?? "根节点";

        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认移动",
            $"节点【{node.Name}】的上级节点：【{oldParentName}】 => 【{newParentName}】!",
            "移动", "取消");
        if (confirmed != true) return;

        // 移动走专门的 move 路由（含防环校验），UpdateNodeAsync 不改 ParentId
        var ok = await Client.MoveNodeAsync(id, newParentId, newSortOrder: 0);
        if (!ok) return;

        await ReloadAsync();
        await NotifyChangedAsync();
    }

    // ========== 子项排序（克隆 TreeSky.HandleSortAsync：排序对话框 + 批量提交） ==========

    private async Task HandleSortAsync(TreeItemData<string> item)
    {
        var id = item.Value!;

        // 子级可能未加载（节点未展开），先确保拿到子节点
        if (item.Children is not { Count: > 0 })
        {
            item.Children = (await LoadServerData(id)).ToList();
        }

        var children = item.Children!
            .Where(c => c.Value is not null && _nodes.ContainsKey(c.Value))
            .Select(c => _nodes[c.Value!])
            .OrderBy(n => n.SortOrder).ThenBy(n => n.Id)
            .ToList();
        if (children.Count == 0) return;

        var parameters = new DialogParameters
        {
            { nameof(StringNodeSortDialog.Children), children }
        };

        var dialog = await DialogService.ShowAsync<StringNodeSortDialog>(
            "拖拽排序", parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, CloseButton = false, BackdropClick = false });
        var result = await dialog.Result;

        if (result is not { Canceled: false } || result.Data is not List<StringNodeDto> sorted) return;

        // 返回列表的顺序即用户调整后的新顺序（后端按列表位置重新编号 SortOrder）
        var ok = await Client.SortChildrenAsync(id, sorted.Select(n => n.Id).ToList());
        if (!ok) return;

        foreach (var n in sorted)
        {
            if (_nodes.TryGetValue(n.Id, out var meta)) meta.SortOrder = n.SortOrder;
        }
        item.Children = sorted.Select(n => ToItem(n, expanded: false))
            .Cast<ITreeItemData<string>>()
            .ToList();

        await NotifyChangedAsync();
    }

    // ========== 属性对话框（EAV 属性，既有实现） ==========

    private async Task OpenPropertiesAsync(string id)
    {
        var node = RequireNode(id);

        var parameters = new DialogParameters
        {
            { nameof(StringNodePropertiesDialog.NodeId), node.Id },
            { nameof(StringNodePropertiesDialog.NodeName), node.Name },
            { nameof(StringNodePropertiesDialog.EntityType), EntityType }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true
        };

        var dialog = await DialogService.ShowAsync<StringNodePropertiesDialog>(
            "节点属性", parameters, options);

        var result = await dialog.Result;

        if (result is { Canceled: false } && SummaryService.IsEnabled)
        {
            SummaryService.Invalidate(EntityType, node.Id);
            _summaries.Remove(node.Id);
            _summaries[node.Id] = await SummaryService.GetSummaryAsync(EntityType, node.Id);

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task NotifyChangedAsync()
    {
        if (OnTreeChanged.HasDelegate) await OnTreeChanged.InvokeAsync();
    }
}

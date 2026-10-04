using Microsoft.AspNetCore.Components;
using MudBlazor;
using TreeGraph.TreeSky.Models;

namespace TreeGraph.TreeSky.Components.Base;

public partial class TreeSky<TItem>
{
    // ========== 节点展开 ==========
    private async Task ExpandToNodeAsync(string targetId, bool clearSelection = true)
    {
        var path = _items?.GetPathToNode(targetId);

        if (path == null)
        {
            path = await GetAncestorPathFromApiAsync(targetId);
        }

        if (path is not { Count: > 0 }) return;

        // 导航切换目标时清空旧选中；初始挂载沿路径恢复时不能清空
        // （中间渲染会让 MudTreeView 在目标子树挂载前固化空选中并回写）
        if (clearSelection)
        {
            SelectedValue = null;
            _ = SelectedValueChanged.InvokeAsync(null);
            StateHasChanged();
        }

        await _items!.ExpandToNodeAsync(
            path: path,
            loadChildren: LoadChildrenAsync,
            onSelected: value =>
            {
                SelectedValue = value;
                _ = SelectedValueChanged.InvokeAsync(value);
            });

        StateHasChanged();
    }

    private async Task<List<string>?> GetAncestorPathFromApiAsync(string nodeId)
    {
        return await ClientService.GetAncestorPathFromApiAsync(nodeId);
    }

    // ========== 节点点击与导航 ==========
    private async Task ClickItemText(ITreeItemData<TItem> node)
    {
        SelectedValue = node.Value;
        _ = SelectedValueChanged.InvokeAsync(node.Value);

        if (IsSelectDialog)
        {
            // 触发外部回调，由调用方决定导航行为
            await OnClickItemText.InvokeAsync(node);

            return;
        }

        if (node.Value!.Id == RootId) return;
        if (!ClientService.NewPageShow || !node.HasChildren) return;

        if (ShowDialogFunc == null)
        {
            // 记录浏览历史
            History.Push(NavigationManager.Uri, RootId, node.Value?.Id);

            if (node?.Value == null) return;
            Snackbar.Add($"点击{node.Text}", Severity.Info);
            NavigationManager.NavigateTo($"{CurrentPage}/rootId/{node.Value.Id}", forceLoad: true, replace: true);

            return;
        }

        // 触发外部回调，由调用方决定导航行为
        await OnClickItemText.InvokeAsync(node);
    }

    private string? _pendingDeepClickNodeId;

    /// <summary>
    /// 从 ClickNodeId 恢复选中状态（首屏阶段）。
    /// 目标已在首屏节点中时直接选中；深层节点登记到 <see cref="_pendingDeepClickNodeId"/>，
    /// 等首帧渲染（MudTreeView 挂载）完成后再沿祖先路径懒加载展开，
    /// 避免在树视图挂载边界内设置选中值被其初始化流程重置。
    /// </summary>
    private Task SetSelectedAsync()
    {
        if (string.IsNullOrEmpty(ClickNodeId)) return Task.CompletedTask;
        if (_items is not { Count: > 0 }) return Task.CompletedTask;

        var found = FindNodeById(_items, ClickNodeId);
        if (found is not null)
        {
            SelectedValue = found;
            _ = SelectedValueChanged.InvokeAsync(found);
            _lastClickNodeId = ClickNodeId;
            return Task.CompletedTask;
        }

        // 深层节点：延后到首帧渲染后处理；同时阻止 OnParametersSetAsync 重复展开
        _pendingDeepClickNodeId = ClickNodeId;
        _lastClickNodeId = ClickNodeId;
        return Task.CompletedTask;
    }

    // ========== 节点查找（显式栈迭代） ==========
    private TItem? FindNodeById(IEnumerable<ITreeItemData<TItem>> items, string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var stack = new Stack<ITreeItemData<TItem>>(items);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current.Value?.Id == id) return current.Value;

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
                stack.Push(child);
        }

        return null;
    }

    /// <summary>
    /// 从树中查找指定节点的父节点（显式栈 DFS）。
    /// </summary>
    private TItem? GetParentNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) || _items is null) return null;

        var stack = new Stack<ITreeItemData<TItem>>(_items);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (current.Children?.Any(c => c.Value?.Id == nodeId) == true)
                return current.Value;

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
                stack.Push(child);
        }

        return null;
    }

    /// <summary>
    /// 从树中移除指定节点（含从父节点 Children 集合摘除）。
    /// 显式栈迭代；栈帧携带实际列表引用与属主节点，保证深层移除能回写父节点。
    /// </summary>
    public bool RemoveNodeFromParent(List<TreeItemData<TItem>> items, string id)
    {
        if (string.IsNullOrEmpty(id) || items.Count == 0) return false;

        // 用非泛型 IList 作栈帧：根列表是 List<TreeItemData<T>>，
        // 子层列表是 List<ITreeItemData<T>>，两者都实现非泛型 IList，
        // 且帧内持有的是真实引用，深层移除可直接回写。
        var stack = new Stack<(System.Collections.IList Level, ITreeItemData<TItem>? Owner)>();
        stack.Push((items, null));

        while (stack.Count > 0)
        {
            var (level, owner) = stack.Pop();

            for (int i = 0; i < level.Count; i++)
            {
                var node = (ITreeItemData<TItem>)level[i]!;

                if (node.Value?.Id == id)
                {
                    level.RemoveAt(i);

                    // 回写属主（子层列表一定是 List<ITreeItemData>）并更新 HasChildren
                    if (owner is not null)
                    {
                        owner.Children = (List<ITreeItemData<TItem>>)level;
                        if (owner.Value is not null)
                            owner.Value.HasChildren = level.Count > 0;
                    }
                    return true;
                }
            }

            // 逆序压栈保持先左后右
            for (int i = level.Count - 1; i >= 0; i--)
            {
                var node = (ITreeItemData<TItem>)level[i]!;
                if (node.Children is not { Count: > 0 }) continue;

                if (node.Children is not System.Collections.IList childLevel)
                {
                    var materialized = node.Children
                        .OfType<TreeItemData<TItem>>()
                        .ToList<ITreeItemData<TItem>>();
                    node.Children = materialized;
                    childLevel = materialized;
                }

                stack.Push((childLevel, node));
            }
        }

        return false;
    }

    #region 获取所有节点

    /// <summary>
    /// 只读：返回当前已加载的全部节点值（扁平化），不触发任何懒加载。
    /// </summary>
    public List<TItem> GetAllLoadedNodes()
    {
        var result = new List<TItem>();
        if (_items is null) return result;

        var stack = new Stack<ITreeItemData<TItem>>(_items);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current.Value is not null) result.Add(current.Value);

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
                stack.Push(child);
        }

        return result;
    }

    /// <summary>
    /// 确保全部节点已加载后返回扁平列表。会沿树触发懒加载 API 调用（有副作用）；
    /// 只读场景请用 <see cref="GetAllLoadedNodes"/>。
    /// </summary>
    public async Task<List<TItem>> EnsureAllNodesLoadedAsync(CancellationToken ct = default)
    {
        var result = new List<TItem>();
        if (_items is null) return result;

        var queue = new Queue<ITreeItemData<TItem>>(_items);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var current = queue.Dequeue();
            if (current.Value is not null) result.Add(current.Value);

            // 未展开但有子节点 → 触发懒加载
            if (current.Children?.Any() != true && current.Value?.HasChildren == true)
            {
                var children = await LoadChildrenAsync(current.Value);
                ct.ThrowIfCancellationRequested();
                current.Children = children.ToList<ITreeItemData<TItem>>();
            }

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
                queue.Enqueue(child);
        }

        return result;
    }

    #endregion
}

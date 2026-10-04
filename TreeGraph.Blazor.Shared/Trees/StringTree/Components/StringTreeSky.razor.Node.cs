using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Components;

public partial class StringTreeSky
{
    // ============================================================
    // 元数据访问
    // ============================================================

    private StringNodeMeta? GetMeta(string? id)
        => string.IsNullOrEmpty(id) ? null : _metaCache.GetValueOrDefault(id);

    // ============================================================
    // 节点点击
    // ============================================================

    private async Task ClickItemAsync(ITreeItemData<string> node)
    {
        if (string.IsNullOrEmpty(node.Value)) return;

        SelectedValue = node.Value;
        await SelectedValueChanged.InvokeAsync(node.Value);

        // 统一走回调（调用方根据 IsSelectDialog 决定行为）
        await OnClickItemText.InvokeAsync(node);
    }

    // ============================================================
    // 初始选中（含深层节点延迟处理）
    // ============================================================

    private Task SetSelectedAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(ClickNodeId)) return Task.CompletedTask;
        if (_items is not { Count: > 0 }) return Task.CompletedTask;

        var found = FindNodeById(_items, ClickNodeId);
        if (found is not null)
        {
            SelectedValue = ClickNodeId;
            _ = SelectedValueChanged.InvokeAsync(ClickNodeId);
            _lastClickNodeId = ClickNodeId;
            return Task.CompletedTask;
        }

        // 深层节点：延后到首帧渲染后处理
        _pendingDeepClickNodeId = ClickNodeId;
        _lastClickNodeId = ClickNodeId;
        return Task.CompletedTask;
    }

    // ============================================================
    // 沿路径展开到目标节点
    // ============================================================

    private async Task ExpandToNodeAsync(
        string targetId, bool clearSelection = true,
        CancellationToken ct = default)
    {
        // 1. 先试已加载的 _items
        var path = _items is not null
            ? GetPathToNode(_items, targetId)
            : null;

        // 2. 未找到 → 走 API 祖先路径
        if (path is null)
        {
            path = await DataSource.GetAncestorPathAsync(targetId, ct);
        }

        ct.ThrowIfCancellationRequested();
        if (path is not { Count: > 0 }) return;

        // 3. 清空旧选中（首次挂载恢复时不清，避免 MudTreeView 首帧覆盖）
        if (clearSelection)
        {
            SelectedValue = null;
            _ = SelectedValueChanged.InvokeAsync(null);
            StateHasChanged();
        }

        // 4. 沿路径逐层展开
        await ExpandPathAsync(path, ct);

        StateHasChanged();
    }

    private async Task ExpandPathAsync(List<string> path, CancellationToken ct)
    {
        var currentItems = _items;
        if (currentItems is null) return;

        for (int i = 0; i < path.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var nodeId = path[i];
            var item = currentItems.FirstOrDefault(x => x.Value == nodeId);
            if (item is null) break;

            // 目标节点：只选中，不展开
            if (i == path.Count - 1)
            {
                SelectedValue = nodeId;
                _ = SelectedValueChanged.InvokeAsync(nodeId);
                break;
            }

            // 中间节点：展开 + 加载子节点
            item.Expanded = true;

            if (item.Children is not { Count: > 0 } && item.Expandable)
            {
                var children = await DataSource.GetChildrenAsync(nodeId, ct);
                ct.ThrowIfCancellationRequested();

                item.Children = children
                    .Select(ToTreeItemData)
                    .ToList<ITreeItemData<string>>();
            }

            currentItems = item.Children?
                .OfType<TreeItemData<string>>()
                .ToList() ?? new List<TreeItemData<string>>();
        }
    }

    // ============================================================
    // 查找（显式栈迭代）
    // ============================================================

    private TreeItemData<string>? FindNodeById(
        IEnumerable<TreeItemData<string>> items, string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var stack = new Stack<TreeItemData<string>>(items);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current.Value == id) return current;

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
            {
                if (child is TreeItemData<string> tc) stack.Push(tc);
            }
        }

        return null;
    }

    /// <summary>BFS + 父指针回溯，返回从根到目标的 ID 路径。</summary>
    private static List<string>? GetPathToNode(
        IEnumerable<TreeItemData<string>> items, string targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return null;

        var parentMap = new Dictionary<string, string?>();
        var queue = new Queue<TreeItemData<string>>();

        foreach (var root in items)
        {
            if (string.IsNullOrEmpty(root.Value)) continue;
            parentMap.TryAdd(root.Value, null);
            queue.Enqueue(root);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (string.IsNullOrEmpty(current.Value)) continue;

            if (current.Value == targetId)
            {
                var path = new List<string>();
                string? cursor = targetId;
                while (cursor is not null)
                {
                    path.Add(cursor);
                    cursor = parentMap.TryGetValue(cursor, out var p) ? p : null;
                }
                path.Reverse();
                return path;
            }

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
            {
                if (child is not TreeItemData<string> { Value: not null } tc) continue;
                if (parentMap.ContainsKey(tc.Value)) continue;   // 防环

                parentMap[tc.Value] = current.Value;
                queue.Enqueue(tc);
            }
        }

        return null;
    }

    // ============================================================
    // 移除节点（迭代 + 深层回写）
    // ============================================================

    public bool RemoveNodeFromParent(List<TreeItemData<string>> items, string id)
    {
        if (string.IsNullOrEmpty(id) || items.Count == 0) return false;

        var stack = new Stack<(System.Collections.IList Level, ITreeItemData<string>? Owner)>();
        stack.Push((items, null));

        while (stack.Count > 0)
        {
            var (level, owner) = stack.Pop();

            for (int i = 0; i < level.Count; i++)
            {
                var node = (ITreeItemData<string>)level[i]!;
                if (node.Value == id)
                {
                    level.RemoveAt(i);

                    if (owner is not null)
                    {
                        owner.Children = (List<ITreeItemData<string>>)level;
                    }
                    return true;
                }
            }

            for (int i = level.Count - 1; i >= 0; i--)
            {
                var node = (ITreeItemData<string>)level[i]!;
                if (node.Children is not { Count: > 0 }) continue;

                if (node.Children is not System.Collections.IList childLevel)
                {
                    var materialized = node.Children
                        .OfType<TreeItemData<string>>()
                        .ToList<ITreeItemData<string>>();
                    node.Children = materialized;
                    childLevel = materialized;
                }

                stack.Push((childLevel, node));
            }
        }

        return false;
    }

    // ============================================================
    // 全量节点
    // ============================================================

    /// <summary>只读：返回当前已加载的全部节点 meta，不触发懒加载。</summary>
    public List<StringNodeMeta> GetAllLoadedNodes()
    {
        var result = new List<StringNodeMeta>();
        if (_items is null) return result;

        var stack = new Stack<TreeItemData<string>>(_items);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (!string.IsNullOrEmpty(current.Value))
            {
                var meta = GetMeta(current.Value);
                if (meta is not null) result.Add(meta);
            }

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
            {
                if (child is TreeItemData<string> tc) stack.Push(tc);
            }
        }

        return result;
    }

    /// <summary>确保全部节点已加载后返回扁平 meta 列表（有副作用）。</summary>
    public async Task<List<StringNodeMeta>> EnsureAllNodesLoadedAsync(
        CancellationToken ct = default)
    {
        var result = new List<StringNodeMeta>();
        if (_items is null) return result;

        var queue = new Queue<TreeItemData<string>>(_items);

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var current = queue.Dequeue();

            if (!string.IsNullOrEmpty(current.Value))
            {
                var meta = GetMeta(current.Value);
                if (meta is not null) result.Add(meta);

                // 未展开但有子节点 → 懒加载
                if (current.Children?.Any() != true && meta?.HasChildren == true)
                {
                    var children = await DataSource.GetChildrenAsync(current.Value, ct);
                    ct.ThrowIfCancellationRequested();

                    current.Children = children
                        .Select(ToTreeItemData)
                        .ToList<ITreeItemData<string>>();
                }
            }

            if (current.Children is not { Count: > 0 }) continue;
            foreach (var child in current.Children)
            {
                if (child is TreeItemData<string> tc) queue.Enqueue(tc);
            }
        }

        return result;
    }
}

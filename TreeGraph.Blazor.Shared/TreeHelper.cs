using MudBlazor;
using TreeGraph.Blazor.Shared.Models;

namespace TreeGraph.Blazor.Shared;

/// <summary>
/// TreeSky 树节点图标与 TreeItemData 转换/查找扩展。
///
/// 遍历/查找全部使用显式栈/队列迭代，深树不会 StackOverflowException。
/// </summary>
public static class TreeHelper
{
    public const string TreeItemIcons = Icons.Material.Outlined.Label;

    public static TreeItemData<T> ToTreeItemData<T>(this TreeNodeDto<T> dto)
        where T : class, ITreeNodeBase<T>, new()
    {
        var item = new TreeItemData<T>
        {
            Icon = TreeItemIcons,
            Text = dto.Text,
            Value = dto.Value,
            Expanded = dto.Expanded,
            Selected = dto.Selected,
            Expandable = dto.HasChildren,
            Children = dto.Children?.Select(i => i.ToTreeItemData<T>()).ToList(),
        };

        // dto.Value 可能为 null（懒加载占位 / 后端返回空值）
        if (item.Value is not null)
            item.Value.Parent = dto.Parent;

        return item;
    }

    /// <summary>
    /// 在树中查找指定 ID 的 TreeItemData 节点（显式栈 DFS）。
    /// </summary>
    public static TreeItemData<T>? FindTreeItem<T>(
        this IEnumerable<TreeItemData<T>> items,
        string id)
        where T : class, ITreeNodeBase<T>
    {
        if (string.IsNullOrEmpty(id)) return null;

        var stack = new Stack<TreeItemData<T>>(items.Reverse());

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (current.Value?.Id == id)
                return current;

            if (current.Children is not { Count: > 0 }) continue;

            // 逆序压栈，保持与原递归一致的先左后右访问顺序
            var children = current.Children.OfType<TreeItemData<T>>().ToList();
            for (int i = children.Count - 1; i >= 0; i--)
                stack.Push(children[i]);
        }

        return null;
    }

    /// <summary>
    /// 获取从根到目标节点的 ID 路径（根在前）。BFS + 父指针回溯，不递归。
    /// 节点若形成环，按已访问集合安全跳过。
    /// </summary>
    public static List<string>? GetPathToNode<T>(
        this IEnumerable<TreeItemData<T>> items,
        string targetId)
        where T : class, ITreeNodeBase<T>
    {
        if (string.IsNullOrEmpty(targetId)) return null;

        var parentMap = new Dictionary<string, string?>();
        var queue = new Queue<TreeItemData<T>>();

        foreach (var root in items)
        {
            if (root.Value is null) continue;
            parentMap.TryAdd(root.Value.Id, null);
            queue.Enqueue(root);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Value is null) continue;

            if (current.Value.Id == targetId)
            {
                var path = new List<string>();
                string? cursor = targetId;
                while (cursor is not null)
                {
                    path.Add(cursor);
                    cursor = parentMap.TryGetValue(cursor, out var parentId) ? parentId : null;
                }
                path.Reverse();
                return path;
            }

            if (current.Children is not { Count: > 0 }) continue;

            foreach (var child in current.Children)
            {
                if (child is not TreeItemData<T> { Value: not null } treeChild) continue;
                if (parentMap.ContainsKey(treeChild.Value.Id)) continue; // 防环

                parentMap[treeChild.Value.Id] = current.Value.Id;
                queue.Enqueue(treeChild);
            }
        }

        return null;
    }

    /// <summary>
    /// 展开指定节点并加载其子节点（单层）。
    /// </summary>
    public static async Task ExpandAsync<T>(
        this TreeItemData<T> item,
        Func<T?, Task<IReadOnlyCollection<TreeItemData<T>>>> loadChildren,
        CancellationToken ct = default)
        where T : class, ITreeNodeBase<T>
    {
        item.Expanded = true;

        if (item.Children == null || item.Children.Count == 0)
        {
            var children = await loadChildren(item.Value);
            ct.ThrowIfCancellationRequested();

            // TreeItemData<T> 未重写 Equals/GetHashCode，
            // ToHashSet 退化为按引用去重，等价于 ToList。
            item.Children = children.ToList<ITreeItemData<T>>();
        }
    }

    /// <summary>
    /// 沿给定路径逐层展开到目标节点（路径迭代，不递归）。
    /// </summary>
    public static async Task ExpandToNodeAsync<T>(
        this List<TreeItemData<T>> items,
        List<string> path,
        Func<T?, Task<IReadOnlyCollection<TreeItemData<T>>>> loadChildren,
        Action<T?>? onSelected = null,
        CancellationToken ct = default)
        where T : class, ITreeNodeBase<T>
    {
        var currentItems = items;

        for (int i = 0; i < path.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var nodeId = path[i];
            var item = currentItems.FirstOrDefault(x => x.Value?.Id == nodeId);
            if (item == null) break;

            // 目标节点本身只选中，不展开/加载它的子节点
            if (i == path.Count - 1)
            {
                onSelected?.Invoke(item.Value);
                break;
            }

            await item.ExpandAsync(loadChildren, ct);

            currentItems = item.Children?
                .OfType<TreeItemData<T>>()
                .ToList() ?? new List<TreeItemData<T>>();
        }
    }
}

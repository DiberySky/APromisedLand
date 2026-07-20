using APromisedLand.Shared.DiberyTree.Interfaces;
using MudBlazor;

namespace APromisedLand.Razor.DiberyTree;

public static class TreeItemDataExtensions
{
    /// <summary>
    /// 在树中查找指定 ID 的 TreeItemData 节点
    /// </summary>
    public static TreeItemData<T>? FindTreeItem<T>(
        this IEnumerable<TreeItemData<T>> items, 
        string id) 
        where T : class, ITreeNode
    {
        foreach (var item in items)
        {
            if (item.Value?.Id == id)
                return item;

            if (item.Children?.Count > 0)
            {
                var found = item.Children
                    .OfType<TreeItemData<T>>()
                    .ToList()
                    .FindTreeItem(id);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

    /// <summary>
    /// 获取从根到目标节点的 ID 路径
    /// </summary>
    public static List<string>? GetPathToNode<T>(
        this IEnumerable<TreeItemData<T>> items, 
        string targetId) 
        where T : class, ITreeNode
    {
        foreach (var item in items)
        {
            if (item.Value?.Id == targetId)
                return new List<string> { targetId };

            if (item.Children?.Count > 0)
            {
                var subPath = item.Children
                    .OfType<TreeItemData<T>>()
                    .ToList()
                    .GetPathToNode(targetId);
                    
                if (subPath != null)
                {
                    subPath.Insert(0, item.Value!.Id);
                    return subPath;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 展开指定节点并加载其子节点（单层）
    /// </summary>
    public static async Task ExpandAsync<T>(
        this TreeItemData<T> item,
        Func<T?, Task<IReadOnlyCollection<TreeItemData<T>>>> loadChildren) 
        where T : class, ITreeNode
    {
        item.Expanded = true;

        if (item.Children == null || item.Children.Count == 0)
        {
            var children = await loadChildren(item.Value);
            item.Children = children.ToHashSet<ITreeItemData<T>>();
        }
    }

    /// <summary>
    /// 递归展开到指定节点（需要路径）
    /// </summary>
    public static async Task ExpandToNodeAsync<T>(
        this List<TreeItemData<T>> items,
        List<string> path,
        Func<T?, Task<IReadOnlyCollection<TreeItemData<T>>>> loadChildren,
        Action<T?>? onSelected = null) 
        where T : class, ITreeNode
    {
        var currentItems = items;

        for (int i = 0; i < path.Count; i++)
        {
            var nodeId = path[i];
            var item = currentItems.FirstOrDefault(x => x.Value?.Id == nodeId);
            if (item == null) break;

            // 展开并加载子节点
            await item.ExpandAsync(loadChildren);

            // 最后一个节点：触发选中回调
            if (i == path.Count - 1)
            {
                onSelected?.Invoke(item.Value);
            }
            else
            {
                // 继续深入下一层
                currentItems = item.Children?
                    .OfType<TreeItemData<T>>()
                    .ToList() ?? new List<TreeItemData<T>>();
            }
        }
    }
}
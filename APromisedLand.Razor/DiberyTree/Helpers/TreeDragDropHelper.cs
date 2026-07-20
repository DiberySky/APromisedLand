using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Razor.DiberyTree.Helpers;

/// <summary>
/// 树形拖拽排序辅助类
/// </summary>
public static class TreeDragDropHelper
{
    /// <summary>
    /// 检查是否可以将源节点拖放到目标节点
    /// </summary>
    public static bool CanDrop<TItem>(TItem source, TItem target, DropPosition position)
        where TItem : class, ITreeNode
    {
        if (source.Id == target.Id) return false;
        if (IsDescendant(source, target)) return false;
        return true;
    }

    /// <summary>
    /// 检查 target 是否是 source 的后代
    /// </summary>
    private static bool IsDescendant<TItem>(TItem source, TItem target)
        where TItem : class, ITreeNode
    {
        // 通过反射获取 Children 属性（因为 ITreeNode 接口没有定义 Children）
        var childrenProperty = source.GetType().GetProperty("Children");
        if (childrenProperty == null) return false;

        var children = childrenProperty.GetValue(source) as IEnumerable<object>;
        if (children == null) return false;

        foreach (var child in children)
        {
            if (child is TItem childItem)
            {
                if (childItem.Id == target.Id) return true;
                if (IsDescendant(childItem, target)) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 计算拖拽后的新排序序号
    /// </summary>
    public static int CalculateNewSortOrder<TItem>(
        List<TItem> siblings,
        TItem draggedItem,
        TItem? targetItem,
        DropPosition position)
        where TItem : class, ITreeNode
    {
        if (targetItem == null) return siblings.Count;

        var targetIndex = siblings.FindIndex(x => x.Id == targetItem.Id);
        if (targetIndex < 0) return siblings.Count;

        return position switch
        {
            DropPosition.Before => targetIndex,
            DropPosition.After => targetIndex + 1,
            DropPosition.Inside => siblings.Count, // 放入内部时排最后
            _ => siblings.Count
        };
    }

    /// <summary>
    /// 重新计算同级节点的排序序号
    /// </summary>
    public static void ReorderSiblings<TItem>(List<TItem> siblings)
        where TItem : class, ITreeNode
    {
        for (int i = 0; i < siblings.Count; i++)
        {
            // 通过反射设置 SortOrder（因为 ITreeNode 接口没有定义 SortOrder）
            var sortOrderProperty = siblings[i].GetType().GetProperty("SortOrder");
            sortOrderProperty?.SetValue(siblings[i], i);
        }
    }
}

/// <summary>
/// 拖拽放置位置
/// </summary>
public enum DropPosition
{
    /// <summary>放在目标节点之前（同级）</summary>
    Before,
    /// <summary>放在目标节点之后（同级）</summary>
    After,
    /// <summary>放入目标节点内部（成为子节点）</summary>
    Inside
}

using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Razor.DiberyTree.Models;

/// <summary>
/// 节点排序结果
/// </summary>
/// <typeparam name="TItem">节点类型</typeparam>
public class SortResult<TItem> where TItem : class, ITreeNode
{
    /// <summary>是否确认保存</summary>
    public bool IsConfirmed { get; set; }

    /// <summary>排序后的节点列表（扁平化，包含新的 SortOrder 和 ParentId）</summary>
    public List<SortItem<TItem>> SortedItems { get; set; } = new();

    /// <summary>是否有层级变更（移动到其他父节点下）</summary>
    public bool HasHierarchyChanges => SortedItems.Any(i => i.IsParentChanged);

    /// <summary>是否有顺序变更</summary>
    public bool HasOrderChanges => SortedItems.Any(i => i.IsOrderChanged);
}

/// <summary>
/// 排序项详情
/// </summary>
public class SortItem<TItem> where TItem : class, ITreeNode
{
    /// <summary>节点</summary>
    public required TItem Node { get; set; }

    /// <summary>新的排序序号</summary>
    public int NewSortOrder { get; set; }

    /// <summary>新的父节点ID（null 表示根节点）</summary>
    public string? NewParentId { get; set; }

    /// <summary>原始排序序号</summary>
    public int OriginalSortOrder { get; set; }

    /// <summary>原始父节点ID</summary>
    public string? OriginalParentId { get; set; }

    /// <summary>是否父节点变更</summary>
    public bool IsParentChanged => NewParentId != OriginalParentId;

    /// <summary>是否顺序变更</summary>
    public bool IsOrderChanged => NewSortOrder != OriginalSortOrder;
}

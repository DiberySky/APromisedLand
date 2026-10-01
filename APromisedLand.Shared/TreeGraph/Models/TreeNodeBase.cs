namespace APromisedLand.Shared.TreeGraph.Models;

/// <summary>
/// 泛型树节点抽象基类,采用 CRTP 自引用泛型,使子类强类型化返回自身。
/// </summary>
public abstract class TreeNodeBase<T, TKey> : ITreeNode<TKey>
    where T : TreeNodeBase<T, TKey>
    where TKey : notnull
{
    public TKey Id { get; set; } = default!;
    public TKey? ParentId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool HasChildren { get; set; }
}

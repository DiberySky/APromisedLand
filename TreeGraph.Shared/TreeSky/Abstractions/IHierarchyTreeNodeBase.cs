namespace TreeGraph.Shared.TreeSky.Abstractions;

/// <summary>
/// 携带层级深度信息的树节点契约。
/// </summary>
public interface IHierarchyTreeNodeBase<TItem> : ITreeNodeBase<TItem>
{
    /// <summary>节点深度（从 0 开始，根节点为 0）</summary>
    int Depth { get; }
}

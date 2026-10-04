namespace TreeGraph.Blazor.Shared.Trees.TreeSky.Models;

/// <summary>
/// 有层级信息的树节点接口
/// </summary>
public interface IHierarchyTreeNodeBase<TItem> : ITreeNodeBase<TItem>
{
    /// <summary>节点深度（从0开始，根节点为0）</summary>
    int Depth { get; }
}

namespace APromisedLand.Shared.DiberyTree.Interfaces;

/// <summary>
/// 有层级信息的树节点接口
/// </summary>
public interface IHierarchyTreeNodeBase : ITreeNodeBase
{
    /// <summary>父节点ID</summary>
    string? ParentId { get; set; }

    /// <summary>节点深度（从0开始，根节点为0）</summary>
    int Depth { get; }
}

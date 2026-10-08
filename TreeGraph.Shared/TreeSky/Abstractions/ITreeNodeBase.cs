namespace TreeGraph.Shared.TreeSky.Abstractions;

/// <summary>
/// 树节点实体的基础契约（泛型）。
/// </summary>
/// <typeparam name="TItem">节点实体类型</typeparam>
public interface ITreeNodeBase<TItem>
{
    string Id { get; set; }
    string? ParentId { get; set; }

    /// <summary>节点描述</summary>
    string? Description { get; set; }

    /// <summary>是否允许有子节点</summary>
    bool CanHaveChildren { get; set; }

    int SortOrder { get; set; }

    bool HasChildren { get; set; }

    TItem? Parent { get; set; }

    string Text();
}

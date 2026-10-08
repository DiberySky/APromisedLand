namespace TreeGraph.Shared.TreeSky.Abstractions;

/// <summary>
/// 可归档的树节点契约。
/// </summary>
public interface IArchivableTreeNodeBase<TItem> : ITreeNodeBase<TItem>
{
    /// <summary>是否已归档</summary>
    bool IsArchived { get; set; }
}

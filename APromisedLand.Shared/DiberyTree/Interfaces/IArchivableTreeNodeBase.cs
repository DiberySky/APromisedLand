namespace APromisedLand.Shared.DiberyTree.Interfaces;

/// <summary>
/// 可归档的树节点接口
/// </summary>
public interface IArchivableTreeNodeBase : ITreeNodeBase
{
    /// <summary>是否已归档</summary>
    bool IsArchived { get; set; }
}

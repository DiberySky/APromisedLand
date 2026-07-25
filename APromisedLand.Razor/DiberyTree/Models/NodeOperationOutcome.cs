using APromisedLand.Razor.DiberyTree.Enums;
using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Razor.DiberyTree.Models;

public class NodeOperationOutcome<TItem> where TItem : class, ITreeNodeBase
{
    /// <summary>用户选择的操作</summary>
    public required NodeAction Action { get; set; }

    /// <summary>目标节点</summary>
    public required TItem Node { get; set; }
}

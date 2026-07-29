using APromisedLand.Razor.DiberyTree.Enums;
using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Razor.DiberyTree.Models;

/// <summary>
/// 节点操作结果
/// </summary>
/// <typeparam name="TItem">节点类型</typeparam>
public class NodeActionResult<TItem> where TItem : class, ITreeNodeBase<TItem>
{
    /// <summary>操作类型</summary>
    public NodeAction Action { get; set; }

    /// <summary>目标节点</summary>
    public required TItem Node { get; set; }
}

using TreeGraph.Blazor.Shared.TreeEavSky.Models;
using TreeGraph.Shared.TreeEavSky.Models;
using TreeGraph.Shared.TreeEavSky.Abstractions;

namespace TreeGraph.Blazor.Shared.TreeEavSky.Contracts;
public class NodeOperationOutcome<TItem> where TItem : class, ITreeNodeBase<TItem>
{
    /// <summary>用户选择的操作</summary>
    public required NodeAction Action { get; set; }

    /// <summary>目标节点</summary>
    public required TItem Node { get; set; }
}

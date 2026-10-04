namespace TreeGraph.Blazor.Shared.Trees.StringTree.Models;

/// <summary>
/// StringTreeSky 的节点操作类型。
/// 与 Trees.Contracts.NodeAction 语义一致，独立定义便于未来分叉。
/// </summary>
public enum StringNodeAction
{
    /// <summary>查看详情</summary>
    View,

    /// <summary>创建子项</summary>
    AddChild,

    /// <summary>编辑节点</summary>
    Edit,

    /// <summary>删除节点（含后代）</summary>
    Delete,

    /// <summary>移动节点到其它父节点</summary>
    Move,

    /// <summary>重排子节点顺序</summary>
    Sort,
}

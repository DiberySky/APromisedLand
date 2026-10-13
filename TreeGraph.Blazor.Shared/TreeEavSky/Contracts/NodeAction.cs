namespace TreeGraph.Blazor.Shared.TreeEavSky.Contracts;
/// <summary>
/// 树节点操作类型
/// </summary>
public enum NodeAction
{
    /// <summary>查看详情</summary>
    View,
    /// <summary>创建子项</summary>
    AddChild,
    /// <summary>编辑节点</summary>
    Edit,
    /// <summary>删除节点</summary>
    Delete,
    /// <summary>移动节点</summary>
    Move,
    /// <summary>排序节点</summary>
    Sort,
    /// <summary>属性（独立属性管理对话框，与基础字段编辑解耦）</summary>
    Attribute
}

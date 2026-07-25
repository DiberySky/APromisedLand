using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Razor.DiberyTree;

/// <summary>
/// 节点对话框操作模式
/// </summary>
public enum NodeDialogMode
{
    /// <summary>浏览节点详情</summary>
    View,
    /// <summary>创建新节点</summary>
    Create,
    /// <summary>编辑节点</summary>
    Edit,
    /// <summary>删除节点</summary>
    Delete
}

/// <summary>
/// 节点对话框操作结果
/// </summary>
/// <typeparam name="TItem">节点类型</typeparam>
public class TreeNodeDialogResult<TItem> where TItem : class, ITreeNodeBase
{
    /// <summary>操作模式</summary>
    public NodeDialogMode Mode { get; set; }

    /// <summary>当前节点（编辑/查看/删除时）</summary>
    public TItem? Node { get; set; }

    /// <summary>父节点（创建时）</summary>
    public TItem? ParentNode { get; set; }

    /// <summary>变更数据字典（创建/编辑时填充）</summary>
    public Dictionary<string, object?>? Changes { get; set; }

    /// <summary>是否成功</summary>
    public bool Success { get; set; } = true;

    /// <summary>错误消息</summary>
    public string? ErrorMessage { get; set; }
}
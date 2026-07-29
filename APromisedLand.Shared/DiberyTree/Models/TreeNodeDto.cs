namespace APromisedLand.Shared.DiberyTree.Models;

/// <summary>
/// 树节点数据传输对象（泛型）
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
public class TreeNodeDto<T>
{
    /// <summary>
    /// 节点唯一标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 父节点ID（根节点为 null）
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// 节点值（泛型）
    /// </summary>
    public T? Value { get; set; }

    /// <summary>
    /// 节点值（泛型）
    /// </summary>
    public T? Parent { get; set; }

    /// <summary>
    /// 显示文本
    /// </summary>
    public string? Text { get; set; }
    
    /// <summary>排序序号，数值越小排序越靠前</summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// 图标
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 是否有子节点
    /// </summary>
    public bool HasChildren { get; set; }

    /// <summary>
    /// 是否展开
    /// </summary>
    public bool Expanded { get; set; }

    /// <summary>
    /// 是否选中
    /// </summary>
    public bool Selected { get; set; }

    /// <summary>
    /// 子节点列表（完整加载时使用）
    /// </summary>
    public List<TreeNodeDto<T>>? Children { get; set; }

    // /// <summary>
    // /// 子节点列表（完整加载时使用）
    // /// </summary>
    // public T? Parent { get; set; }

    /// <summary>
    /// 额外数据（扩展字段）
    /// </summary>
    public Dictionary<string, object>? ExtraData { get; set; }
}

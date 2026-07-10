namespace APromisedLand.Api.Projects.DiberyTree.Models;

/// <summary>
/// MudBlazor 内置的树节点数据模型
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
public class TreeItemData<T>
{
    /// <summary>
    /// 节点的值（泛型，可以是任意类型）
    /// </summary>
    public T? Value { get; set; }

    /// <summary>
    /// 子节点集合
    /// </summary>
    public List<TreeItemData<T>> Children { get; set; } = new();

    /// <summary>
    /// 是否包含子节点（用于懒加载时显示展开图标）
    /// </summary>
    public bool HasChildren { get; set; }

    /// <summary>
    /// 图标（可选）
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 是否展开
    /// </summary>
    public bool Expanded { get; set; }

    /// <summary>
    /// 是否选中
    /// </summary>
    public bool Selected { get; set; }
}

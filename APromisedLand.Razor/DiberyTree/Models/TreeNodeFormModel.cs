namespace APromisedLand.Razor.DiberyTree.Models;

/// <summary>
/// 树节点表单模型
/// </summary>
public class TreeNodeFormModel
{
    /// <summary>节点名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>节点描述</summary>
    public string? Description { get; set; }

    /// <summary>排序序号</summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;
}

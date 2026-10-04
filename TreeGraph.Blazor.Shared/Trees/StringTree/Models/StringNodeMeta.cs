namespace TreeGraph.Blazor.Shared.Trees.StringTree.Models;

/// <summary>
/// T=string 树节点的元数据。
///
/// ★ Value 是 GUID（不可读标识），Text 是显示名——两者分离。
///   MudTreeView&lt;string&gt; 承载 Value，Text / Icon 通过 TreeItemData 携带。
/// </summary>
public class StringNodeMeta
{
    /// <summary>节点唯一 ID（GUID 字符串，即 MudTreeView&lt;string&gt; 的 Value）</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>父节点 ID（null = 根节点）</summary>
    public string? ParentId { get; set; }

    /// <summary>显示文本</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>描述（详情对话框展示）</summary>
    public string? Description { get; set; }

    /// <summary>副标题（可选）</summary>
    public string? Subtitle { get; set; }

    /// <summary>MudBlazor 图标常量（可选）</summary>
    public string? Icon { get; set; }

    /// <summary>是否实际有子节点（决定展开箭头）</summary>
    public bool HasChildren { get; set; }

    /// <summary>
    /// 是否允许添加子节点。
    /// false 时：即使有子节点也不允许"创建子项"（如系统分类不可扩展）。
    /// </summary>
    public bool CanHaveChildren { get; set; } = true;

    /// <summary>排序序号（越小越靠前）</summary>
    public int SortOrder { get; set; }

    /// <summary>扩展数据（业务自定义字段）</summary>
    public Dictionary<string, object?>? ExtraData { get; set; }

    /// <summary>浅拷贝（用于编辑模板 / 对话框传值不污染原对象）。</summary>
    public StringNodeMeta Clone() => new()
    {
        Id = Id,
        ParentId = ParentId,
        Text = Text,
        Description = Description,
        Subtitle = Subtitle,
        Icon = Icon,
        HasChildren = HasChildren,
        CanHaveChildren = CanHaveChildren,
        SortOrder = SortOrder,
        ExtraData = ExtraData is null
            ? null
            : new Dictionary<string, object?>(ExtraData),
    };
}

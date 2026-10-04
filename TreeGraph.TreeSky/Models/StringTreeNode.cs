using System.Text.Json.Serialization;
using TreeGraph.TreeSky.Attributes;

namespace TreeGraph.TreeSky.Models;

/// <summary>
/// 库内建的“字符串节点”类型：以 <see cref="Name"/> 字符串作为显示文本，
/// 满足 TreeSky 泛型约束 class, ITreeNodeBase&lt;T&gt;, new()。
/// （System.String 为 sealed 且无无参构造，无法直接作为 TItem，故用此类承载。）
/// 宿主可直接使用，也可继承后扩展字段。
/// </summary>
/// <remarks>
/// 路由值与类型名一致：后端 StringTreeNodeController 使用 [Route("[controller]")]，
/// 显式声明后类型改名不会漂移 URL 契约。
/// </remarks>
[TreeRoute("StringTreeNode")]
public class StringTreeNode : ITreeNodeBase<StringTreeNode>
{
    public string Id { get; set; } = string.Empty;

    /// <summary>节点名称（显示字符串）</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool CanHaveChildren { get; set; } = true;

    public int SortOrder { get; set; }

    public bool HasChildren { get; set; }

    public string? ParentId { get; set; }

    /// <summary>父节点导航属性：仅前端组件内部赋值；JSON 传输时忽略，避免循环引用。</summary>
    [JsonIgnore]
    public StringTreeNode? Parent { get; set; }

    /// <summary>
    /// 子节点导航属性：仅供父节点选择对话框反射使用；
    /// JSON 传输时忽略，避免与 <see cref="Parent"/> 形成循环引用。
    /// </summary>
    [JsonIgnore]
    public List<StringTreeNode> Children { get; set; } = [];

    public string Text() => Name;
}

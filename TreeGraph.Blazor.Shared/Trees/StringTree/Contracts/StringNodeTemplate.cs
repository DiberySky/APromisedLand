using Microsoft.AspNetCore.Components;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Contracts;

/// <summary>
/// 节点模板容器：把"目标节点 + 编辑 UI"绑定到一起。
/// 与 Trees.Contracts.NodeTemplate&lt;TItem&gt; 对应。
/// </summary>
public class StringNodeTemplate
{
    /// <summary>目标节点元数据（引用原对象）。</summary>
    public required StringNodeMeta Node { get; set; }

    /// <summary>节点操作区模板（可选）。</summary>
    public RenderFragment<StringNodeMeta>? ActionTemplate { get; set; }

    /// <summary>节点编辑模板（可选）。</summary>
    public RenderFragment<StringNodeMeta>? EditTemplate { get; set; }
}

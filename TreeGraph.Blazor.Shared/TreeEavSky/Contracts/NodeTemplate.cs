using Microsoft.AspNetCore.Components;
using MudBlazor;
using TreeGraph.Shared.TreeEavSky.Models;
using TreeGraph.Shared.TreeEavSky.Abstractions;

namespace TreeGraph.Blazor.Shared.TreeEavSky.Contracts;
public class NodeTemplate<TItem>
    where TItem : class, ITreeNodeBase<TItem>
{
    /// <summary>目标节点</summary>
    public required TreeNodeItem<TItem> Node { get; set; }

    /// <summary>用户选择的操作</summary>
    public RenderFragment<TItem>? ActionTemplate { get; set; }

    public RenderFragment<TItem>? EditTemplate { get; set; }
}

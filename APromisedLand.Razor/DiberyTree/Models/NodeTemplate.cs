using APromisedLand.Shared.DiberyTree.Interfaces;
using Microsoft.AspNetCore.Components;

namespace APromisedLand.Razor.DiberyTree.Models;

public class NodeOperation<TItem> 
    where TItem : class, ITreeNode
{
    /// <summary>目标节点</summary>
    public required TItem Node { get; set; }

    /// <summary>用户选择的操作</summary>
    public RenderFragment<TItem>? ActionTemplate { get; set; }
    
    
    public static NodeOperation<TItem> SetActionTemplate(TItem node, RenderFragment<TItem>? actionTemplate)
    {
        return new NodeOperation<TItem>{Node = node, ActionTemplate = actionTemplate};
    }
}
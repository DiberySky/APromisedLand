using APromisedLand.Shared.DiberyTree.Interfaces;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace APromisedLand.Razor.DiberyTree.Models;

public class NodeTemplate<TItem> 
    where TItem : class, ITreeNodeBase
{
    /// <summary>目标节点</summary>
    public required  ITreeItemData<TItem> Node { get; set; }

    /// <summary>用户选择的操作</summary>
    public RenderFragment<TItem>? ActionTemplate { get; set; }
    
    public RenderFragment<TItem>? EditTemplate { get; set; }
}
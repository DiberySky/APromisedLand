using TreeGraph.Blazor.Shared.TreeSky.Models;
using TreeGraph.Shared.TreeSky.Models;
using TreeGraph.Shared.TreeSky.Abstractions;

namespace TreeGraph.Blazor.Shared.TreeSky.Contracts;
/// <summary>
/// 父节点选择结果
/// </summary>
/// <typeparam name="TItem">节点类型</typeparam>
public class ParentSelectResult<TItem> where TItem : class, ITreeNodeBase<TItem>
{
    /// <summary>是否确认选择</summary>
    public bool IsConfirmed { get; set; }

    /// <summary>选中的父节点（null 表示根节点）</summary>
    public TItem? SelectedParent { get; set; }

    /// <summary>选中节点的路径ID列表</summary>
    public List<string> SelectedPath { get; set; } = new();
}

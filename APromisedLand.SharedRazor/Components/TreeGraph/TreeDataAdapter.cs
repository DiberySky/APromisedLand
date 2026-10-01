using MudBlazor;
using APromisedLand.Shared.TreeGraph.Models;

namespace APromisedLand.SharedRazor.Components.TreeGraph;

/// <summary>
/// 适配 MudBlazor 9.9.0 的泛型 TreeItemData&lt;T&gt;,承载 ITreeNode&lt;string&gt; 强类型节点。
/// </summary>
public static class TreeDataAdapter
{
    /// <summary>
    /// 将 ITreeNode&lt;string&gt; 集合转换为 MudBlazor 的 TreeItemData&lt;T&gt; 列表(默认折叠)。
    /// </summary>
    public static List<TreeItemData<T>> ToTreeItemData<T>(this IEnumerable<T> nodes)
        where T : class, ITreeNode<string>
    {
        return nodes.Select(n => new TreeItemData<T>
        {
            Text = n.Text,
            Value = n,
            Expandable = n.HasChildren,
            Expanded = false,
            Icon = n.Icon
        }).ToList();
    }
}

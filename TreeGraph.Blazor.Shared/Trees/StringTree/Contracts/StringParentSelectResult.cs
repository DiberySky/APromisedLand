using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Contracts;

/// <summary>父节点选择结果。</summary>
public class StringParentSelectResult
{
    /// <summary>是否确认选择。</summary>
    public bool IsConfirmed { get; set; }

    /// <summary>选中的父节点（null 表示根节点）。</summary>
    public StringNodeMeta? SelectedParent { get; set; }

    /// <summary>选中节点的路径 ID 列表。</summary>
    public List<string> SelectedPath { get; set; } = new();
}

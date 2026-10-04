using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Contracts;

/// <summary>节点操作结果（操作 + 目标节点）。</summary>
public class StringNodeActionResult
{
    public StringNodeAction Action { get; set; }
    public required StringNodeMeta Node { get; set; }
}

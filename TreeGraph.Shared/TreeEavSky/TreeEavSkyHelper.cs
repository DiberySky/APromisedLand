using TreeGraph.Shared.TreeEavSky.Abstractions;
using TreeGraph.Shared.TreeEavSky.Models;

namespace TreeGraph.Shared.TreeEavSky;

/// <summary>
/// TreeSky 实体与 DTO 之间的映射辅助。
/// </summary>
public static class TreeSkyHelper
{
    /// <summary>
    /// 将树节点实体映射为 <see cref="TreeNodeDto{T}"/>。
    /// </summary>
    public static TreeNodeDto<T> ToNodeDto<T>(
        this T nodeValue,
        string? icon = null,
        bool hasChildren = false) where T : class, ITreeNodeBase<T>
    {
        return new TreeNodeDto<T>
        {
            Id = nodeValue.Id,
            Text = nodeValue.Text(),
            ParentId = nodeValue.ParentId,
            Value = nodeValue,
            Icon = icon,
            HasChildren = hasChildren,
            SortOrder = nodeValue.SortOrder,
        };
    }
}

using TreeGraph.Shared.TreeEavSky.Abstractions;
using TreeGraph.Shared.TreeEavSky.Models;

namespace TreeGraph.Shared.TreeEavSky;

/// <summary>
/// TreeEavSky 实体与 DTO 之间的映射辅助。
/// </summary>
public static class TreeEavSkyHelper
{
    /// <summary>
    /// 将树节点实体映射为 <see cref="TreeNodeDto{T}"/>。
    /// </summary>
    /// <param name="text">显示文本（来自 nodes 注册表的名称，UnitTree 含简称拼接）。</param>
    public static TreeNodeDto<T> ToNodeDto<T>(
        this T nodeValue,
        string? text,
        string? icon = null,
        bool hasChildren = false) where T : class, ITreeNodeBase<T>
    {
        return new TreeNodeDto<T>
        {
            Id = nodeValue.Id,
            Text = text,
            ParentId = nodeValue.ParentId,
            Value = nodeValue,
            Icon = icon,
            HasChildren = hasChildren,
            CanHaveChildren = nodeValue.CanHaveChildren,
            SortOrder = nodeValue.SortOrder,
        };
    }
}

using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.DiberyTree.Models;

namespace APromisedLand.Shared.DiberyTree;

public static class DiberyTreeHelper
{
    // public static TreeNodeDto<CategoryTree> ToNodeDto(this CategoryTree category)
    // {
    //     return new TreeNodeDto<CategoryTree>
    //     {
    //         Id = category.Id,
    //         Text = category.Name,
    //         ParentId = category.ParentId,
    //         Value = category
    //     };
    // }
    
    // 通用泛型扩展方法，修正为使用 category 实例
    public static TreeNodeDto<T> ToNodeDto<T>(
        this T nodeValue,
        string? icon = null,
        bool hasChildren = false) where T : class, ITreeNodeBase
    {
        return new TreeNodeDto<T>
        {
            Id = nodeValue.Id,
            Text = nodeValue.Text(),
            ParentId = nodeValue.ParentId,
            Value = nodeValue,
            Icon = icon,
            HasChildren = hasChildren
        };
    }
}
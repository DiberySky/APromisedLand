using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.DiberyTree.Models;
using MudBlazor;

namespace APromisedLand.Razor.Helper.Blazor;

public static partial class BlazorHelper
{
    public const string TreeItemIcons = Icons.Material.Filled.Label;
    
    public static TreeItemData<T> ToTreeItemData<T>(this TreeNodeDto<T> dto)
        where T : class, ITreeNodeBase<T>, new()
    {
        var item = new TreeItemData<T>
        {
            Icon = TreeItemIcons,
            Text = dto.Text,
            Value = dto.Value,
            Expanded = dto.Expanded,
            Selected = dto.Selected,
            Expandable = dto.HasChildren,
            Children = dto.Children?.Select(i => i.ToTreeItemData<T>()).ToList(),
        };
        
        item.Value!.Parent = dto.Parent;
        return item;
    }
}
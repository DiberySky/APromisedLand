using APromisedLand.Shared.DiberyTree.Models;
using MudBlazor;

namespace APromisedLand.Razor.Helper.Blazor;

public static partial class BlazorHelper
{
    public static TreeItemData<T> ToTreeItemData<T>(this TreeNodeDto<T> dto)
    {
        return new TreeItemData<T>
        {
            Icon = Icons.Material.Filled.Label,
            Text = dto.Text,
            Value = dto.Value,
            Expanded = dto.Expanded,
            Selected = dto.Selected,
            Expandable = dto.HasChildren,
            Children = dto.Children?.Select(i => i.ToTreeItemData<T>()).ToList(),
        };
    }
}
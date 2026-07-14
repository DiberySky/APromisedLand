using APromisedLand.Shared.DiberyTree.Models;
using MudBlazor;

namespace APromisedLand.Razor.Helper;

public static partial class BlazorHelper
{
    public static TreeItemData<T> ToTreeItemData<T>(this TreeNodeDto<T> dto)
    {
        return new TreeItemData<T>
        {
            Icon = dto.Icon,
            Text = dto.Text,
            Value = dto.Value,
            Expanded = dto.Expanded,
            Selected = dto.Selected,
            Expandable = dto.Expanded,
        };
    }
}
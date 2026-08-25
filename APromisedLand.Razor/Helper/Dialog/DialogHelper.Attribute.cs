using APromisedLand.Razor.Dialogs;
using APromisedLand.Razor.DiberyTree.Attributes;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using MudBlazor;

namespace APromisedLand.Razor.Helper.Dialog;

public static partial class DialogHelper
{
    public static async Task<AttributeDefinitionDto?> ShowAttributeDefinitionListDialogAsync(this IDialogService dialogService,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters<AttributeDefinitionListDialog>
        {
            { x => x.ReadOnly, true }
        };

        var options = (config ?? new DialogConfig
        {
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<AttributeDefinitionListDialog>(
            "节点属性", parameters, options);


        var result = await dialog.Result;

        // 如果用户取消或关闭对话框，返回 null
        if (result == null || result.Canceled)
            return null;

        // 从 Data 中提取并转换返回值
        return result.Data as AttributeDefinitionDto;
    }
    
    public static async Task<bool?> ShowAttributeDefinitionDialogAsync(this IDialogService dialogService,
        AttributeDefinitionDto? item = null,
        string? tableId  = null,
        DialogConfig? config = null) 
    {
        var parameters = new DialogParameters<AttributeDefinitionDialog>
        {
            { x => x.TableId, tableId },
            { x => x.EditItem, item },
        };

        var options = (config ?? new DialogConfig
        {
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<AttributeDefinitionDialog>(
            "节点属性定义", parameters, options);

        var result = await dialog.Result;
        // 只要不是取消就返回 true（表示可能已修改）
        return result is not { Canceled: true };
    }
}
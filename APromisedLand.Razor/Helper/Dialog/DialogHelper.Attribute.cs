using APromisedLand.Razor.Dialogs;
using APromisedLand.Razor.DiberyTree.Attributes;
using APromisedLand.Razor.DiberyTree.Attributes.Locations;
using APromisedLand.Razor.DiberyTree.Attributes.Tables;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using MudBlazor;

namespace APromisedLand.Razor.Helper.Dialog;

public static partial class DialogHelper
{
    public static async Task ShowAttributeTableRowsPanelDialogAsync(this IDialogService dialogService,
        string nodeId,
        string tableId,
        string tableName,
        AttributeDefinition definitionDto,
        List<AttributeDefinition> columns,
        bool readOnly = false,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters<AttributeTableRowsPanelDialog>
        {
            { x => x.NodeId, nodeId },
            { x => x.TableId, tableId },
            { x => x.TableName, tableName },
            { x => x.TableDefinitionDto, definitionDto },
            { x => x.Columns, columns },
            { x => x.ReadOnly, readOnly },
        };

        var options = (config ?? new DialogConfig
        {
            Position = DialogPosition.Center
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<AttributeTableRowsPanelDialog>(
            "表格行编辑器", parameters, options);

        var result = await dialog.Result;
    }
    
    public static async Task ShowAttributeLocationEditDialogAsync(this IDialogService dialogService,
        string nodeId, object locationId,
        AttributeDefinition? definitionDto,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters<AttributeLocationEditDialog>
        {
            { x => x.NodeId, nodeId },
            { x => x.LocationId, locationId },
            { x => x.DefinitionDto, definitionDto },
        };

        var options = (config ?? new DialogConfig
        {
            Position = DialogPosition.Center
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<AttributeLocationEditDialog>(
            "位置编辑器", parameters, options);

        var result = await dialog.Result;
    }
    
    public static async Task<AttributeDefinition?> ShowAttributeDefinitionListDialogAsync(this IDialogService dialogService,
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
        return result.Data as AttributeDefinition;
    }
    
    public static async Task<bool?> ShowAttributeDefinitionDialogAsync(this IDialogService dialogService,
        AttributeDefinition? item = null,
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
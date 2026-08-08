using APromisedLand.Razor.Dialogs.UnitsOfMeasure;
using APromisedLand.Razor.DiberyTree.Trees.Category;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.DTOs.Units;
using MudBlazor;

namespace APromisedLand.Razor.Dialogs;

public static class DialogHelper
{
    public static async Task ShowCategoryTreeDialogAsync(this IDialogService dialogService, 
        ITreeItemData<CategoryTree>? node = null)
    {
        var parameters = new DialogParameters<CategoryTreeDialogPage>
        {
            { x => x.ClickNode, node },
        };

        var options = new DialogConfig
        {
            FullScreen = true,
            FullWidth = true,
            MaxWidth = MaxWidth.False,
        }.ToDialogOptions();

        var dialog = await dialogService.ShowAsync<CategoryTreeDialogPage>("分类",
            parameters, options);

        var result = await dialog.Result;

        //return result.Canceled == false;
    }
    
    public static async Task<UnitOfMeasureDto?> ShowUnitOfMeasureSelectDialogAsync(this IDialogService dialogService)
    {
        var parameters = new DialogParameters<UnitOfMeasureSelectDialog>
        {
            //{ x => x.TestingArgs, testingArgs }
        };

        var options = new DialogConfig
        {
            FullWidth = true,
            MaxWidth = MaxWidth.Small,
        }.ToDialogOptions();

        var dialog = await dialogService.ShowAsync<UnitOfMeasureSelectDialog>("计量单位",
            parameters, options);

        var result = await dialog.Result;

        if (result is { Canceled: false, Data: UnitOfMeasureDto selected })
        {
            return selected;
        }
        
        return null;
    }
    
    public static async Task ShowUnitOfMeasureDialogAsync(this IDialogService dialogService)
    {
        var parameters = new DialogParameters<UnitsOfMeasureDialogPage>
        {
            //{ x => x.TestingArgs, testingArgs }
        };

        var options = new DialogConfig
        {
            FullScreen = true,
            FullWidth = true,
            MaxWidth = MaxWidth.False,
        }.ToDialogOptions();

        var dialog = await dialogService.ShowAsync<UnitsOfMeasureDialogPage>("计量单位",
            parameters, options);

        var result = await dialog.Result;

        //return result.Canceled == false;
    }
}
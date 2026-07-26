using APromisedLand.Razor.Components;
using APromisedLand.Razor.Helper;
using MudBlazor;

namespace APromisedLand.Razor.Dialogs;

public static class DialogHelper
{
    public static async Task ShowUnitOfMeasureDialogAsync(this IDialogService dialogService)
    {
        var parameters = new DialogParameters<AboutDialog>
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
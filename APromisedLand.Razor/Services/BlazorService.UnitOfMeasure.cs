using APromisedLand.Razor.Dialogs;
using APromisedLand.Razor.Dialogs.UnitsOfMeasure;
using MudBlazor;

namespace APromisedLand.Razor.Services;

public partial class BlazorService
{
    public async Task ShowUnitOfMeasureDialogAsync()
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
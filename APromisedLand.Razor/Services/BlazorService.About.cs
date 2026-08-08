using APromisedLand.Razor.Components;
using MudBlazor;

namespace APromisedLand.Razor.Services;

public partial class BlazorService
{
    public async Task ShowAboutDialogAsync()
    {
        var parameters = new DialogParameters<AboutDialog>
        {
            //{ x => x.TestingArgs, testingArgs }
        };

        var options = new DialogOptions
        {
            FullScreen = false,
            CloseButton = false,
            CloseOnEscapeKey = true,
            FullWidth = true,
            Position = DialogPosition.Center,
            MaxWidth = MaxWidth.Small,
            BackdropClick = true,
            BackgroundClass = null,
            CloseOnNavigation = true,
            DefaultFocus = DefaultFocus.None,
            NoHeader = false,
        };

        var dialog = await dialogService.ShowAsync<AboutDialog>("关于",
            parameters, options);

        var result = await dialog.Result;

        //return result.Canceled == false;
    }
}
using APromisedLand.Razor.Helper.Blazor;
using MudBlazor;

namespace APromisedLand.Razor.Services;

public partial class BlazorService
{
    public void ShowError(
        string message,
        string detail)
    {
        snackbar.Add(message, Severity.Error, config =>
        {
            config.VisibleStateDuration = 7000;
            config.ShowCloseIcon = false;
            config.Action = "查看";
            config.ActionColor = Color.Warning;
            config.ActionVariant = Variant.Filled;
            config.OnClick = async e =>
            {
                await HelpAsync(dialogService, message, detail);
            };
        });
    }

    private static async Task HelpAsync(IDialogService dialogService, 
        string message, string details)
    {
        //snackbar.Add(message);
        var options = new MessageBoxOptions
        {
            Title = message,
            Message = details,
            YesText = "关闭",
        };

        var dialogOptions = new DialogOptions
        {
            BackdropClick = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
            CloseButton = false,
        };

        await dialogService.ShowMessageBoxAsync(options, dialogOptions);
    }

    public void ShowSuccess(string message) //=> snackbar.Add(message, Severity.Success);
    {
        snackbar.Add(message, Severity.Success, config =>
        {
            config.VisibleStateDuration = 3000;
        });
    }
    
    public void ShowInfo(string message) //=> snackbar.Add(message, Severity.Success);
    {
        snackbar.Add(message, Severity.Info, config =>
        {
            config.VisibleStateDuration = 3000;
        });
    }
}
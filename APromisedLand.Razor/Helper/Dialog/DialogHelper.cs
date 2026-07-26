using MudBlazor;
using System;
using System.Collections.Generic;
using System.Text;
using APromisedLand.Razor.Components;

namespace APromisedLand.Razor.Helper;

public static partial class DialogHelper
{
    public static async Task ShowTestDialogAsync(this IDialogService dialogService,
        string title, string testingArgs)
    {
        var parameters = new DialogParameters<TestingDialog>
        {
            { x => x.TestingArgs, testingArgs }
        };

        var options = new DialogOptions
        {
            FullScreen = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            FullWidth = true,
            Position = DialogPosition.Center,
            MaxWidth = MaxWidth.False,
            BackdropClick = false,
            BackgroundClass = null,
            CloseOnNavigation = true,
            DefaultFocus = DefaultFocus.None,
            NoHeader = false,
        };

        var dialog = await dialogService.ShowAsync<TestingDialog>(title,
            parameters, options);

        var result = await dialog.Result;

        //return result.Canceled == false;
    }
}

using MudBlazor;

namespace APromisedLand.Razor.Helper.Blazor;

public static partial class BlazorHelper
{
    public static DialogOptions DialogOptions =>
        new()
        {
            FullScreen = true,
            CloseButton = true,
            BackdropClick = true,
        };
}

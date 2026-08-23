using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace APromisedLand.Razor.Helper.Blazor;

public static partial class BlazorHelper
{
    private static IServiceProvider? _serviceProvider;

    public static void BlazorHelperInitialize(this IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public static DialogOptions DialogOptions =>
        new()
        {
            FullScreen = true,
            CloseButton = true,
            BackdropClick = true,
        };
}

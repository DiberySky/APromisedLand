using MudBlazor;
using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Razor.Helper;

public static partial class BlazorHelper
{
    public static DialogOptions DialogOptions
    {
        get
        {
            return new()
            {
                FullScreen = true,
                CloseButton = true,
                BackdropClick = true,
            };
        }
    }
}

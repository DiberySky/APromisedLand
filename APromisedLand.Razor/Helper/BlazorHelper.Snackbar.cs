using APromisedLand.Shared.Helper;
using MudBlazor;
using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Razor.Helper;

public static partial class BlazorHelper
{
    public static void Normal(this ISnackbar snackbar, string? message)
        => snackbar.Add(message ?? "没有信息。", Severity.Normal,
            config =>
            {
                config.ShowCloseIcon = true;
                config.SnackbarVariant = Variant.Filled;
            });

    public static void Success(this ISnackbar snackbar, string? message)
        => snackbar.Add(message ?? "没有信息。", Severity.Success,
            config =>
            {
                config.ShowCloseIcon = true;
                config.SnackbarVariant = Variant.Filled;
            });

    public static void Info(this ISnackbar snackbar, string? message)
        => snackbar.Add(message ?? "没有信息。", Severity.Info,
            config =>
            {
                config.ShowCloseIcon = true;
                config.SnackbarVariant = Variant.Filled;
            });

    public static void Warning(this ISnackbar snackbar, string? message)
    {
        snackbar.Add(message ?? "没有信息。", Severity.Warning,
            config =>
            {
                config.ShowCloseIcon = true;
                config.SnackbarVariant = Variant.Filled;
            });
    }

    public static void Error(this ISnackbar snackbar, string? message)
    {
        var duration = 3000;
#if DEBUG
        duration = 10000;
        message = $"错误:{message?.Ellipsis(30)}";
#endif
        snackbar.Add(message ?? "没有信息。", Severity.Error,
            config =>
            {
                config.VisibleStateDuration = duration;
                config.SnackbarVariant = Variant.Filled;
            });
    }

    public static void Details(this ISnackbar snackbar, string message, string detail, IDialogService dialogService)
    {
        snackbar.Add(message, Severity.Error, config =>
        {
            config.ShowCloseIcon = true;
            config.Action = "查看";
            config.ActionColor = Color.Info;
            config.ActionVariant = Variant.Filled;
            config.OnClick = snackbar =>
            {
                Help(dialogService, message, detail);
                return Task.CompletedTask;
            };
        });
    }

    private static async void Help(IDialogService dialogService, string message, string details)
    {
        //snackbar.Add(message);
        var options = new MessageBoxOptions
        {
            Title = message,
            Message = details,
        };

        var dialogOptions = new DialogOptions
        {
            BackdropClick = false,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
            CloseButton = true,
        };

        await dialogService.ShowMessageBoxAsync(options, dialogOptions);
    }
}

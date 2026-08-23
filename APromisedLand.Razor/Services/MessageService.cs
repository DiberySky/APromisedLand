using APromisedLand.Razor.Components;
using APromisedLand.Shared.Helper;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Options;

namespace APromisedLand.Razor.Services;

public class MessageService(IDialogService dialogService, ISnackbar snackbar)
{
    public DialogOptionsEx DialogOptions { get; set; } = new DialogOptionsEx
    {
        MaximizeButton = true, // 启用最大化/还原按钮
        CloseButton = false, // 同时显示关闭按钮
        FullWidth = true,
        MaxWidth = MaxWidth.Small,
        BackdropClick = false,
        Position = DialogPosition.Center,
        Resizeable = true,
        DragMode = MudDialogDragMode.Simple,
        AnimateClose = true,
    };

    public async Task<bool> DeleteBoxAsync(string? message, string? title = "删除")
    {
        var options = new MessageBoxOptions
        {
            Title = title,
            Message = message,
            YesText = "是",
            CancelText = "否",
        };

        var dialogOptions = new DialogOptions
        {
            BackdropClick = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
        };

        bool? result = await dialogService.ShowMessageBoxAsync(options, dialogOptions);

        return result ?? false;
    }
    
    // public async Task<bool> DeleteBoxAsync(string message = "删除操作无法撤消！", string title = "警告")
    // {
    //     var result = await dialogService.ShowMessageBoxAsync(
    //         title, message,
    //         yesText: "删除！", cancelText: "取消",
    //         options: new DialogOptions
    //         {
    //             MaxWidth = MaxWidth.ExtraSmall,
    //             BackdropClick = false,
    //             FullWidth = true
    //         });
    //
    //     return result != null;
    // }

    public async Task<bool> BoolBoxAsync(string message = "删除操作无法撤消！", string title = "请确认")
    {
        var result = await dialogService.ShowMessageBoxAsync(
            title, message,
            yesText: "确认！", cancelText: "取消",
            options: new DialogOptions
            {
                MaxWidth = MaxWidth.ExtraSmall,
                BackdropClick = false,
                FullWidth = true
            });

        return result != null;
    }

    //snackbar 通知
    public void Details(
        string message,
        string detail)
    {
        snackbar.Add(message, Severity.Error, config =>
        {
            config.VisibleStateDuration = 3000;
            config.ShowCloseIcon = false;
            config.Action = "查看";
            config.ActionColor = Color.Info;
            config.ActionVariant = Variant.Filled;
            config.OnClick = async e => { await HelpAsync(dialogService, message, detail); };
        });
    }

    private static async Task HelpAsync(IDialogService dialogService,
        string message, string details)
    {
        //snackbar.Add(message);
        var options = new DialogParameters<MessageDialog>()
        {
            { x => x.Title, message },
            { x => x.Message, details },
        };

        var dialogOptions = new DialogOptions
        {
            BackdropClick = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
            CloseButton = false,
        };

        await dialogService.ShowExAsync<MessageDialog>("提示", options, dialogOptions);
    }

    public void Success(string? message)
    {
        snackbar.Add(message ?? "没有信息。", Severity.Success,
            config =>
            {
                config.VisibleStateDuration = 3000;
                config.ShowCloseIcon = true;
                config.SnackbarVariant = Variant.Outlined;
            });
    }

    public void Info(string message) //=> snackbar.Add(message, Severity.Success);
    {
        snackbar.Add(message, Severity.Info, config => { config.VisibleStateDuration = 3000; });
    }
    
        public void Error(string? message)
    {
        var duration = 5000;
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
}
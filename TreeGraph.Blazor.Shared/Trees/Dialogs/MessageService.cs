using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Components;
using MudBlazor.Extensions.Options;

namespace TreeGraph.Blazor.Shared.Trees.Dialogs;
/// <summary>
/// TreeSky 的消息/通知服务（精简版：基于 MudBlazor 原生 MessageBox 与 Snackbar，不依赖自定义 MessageDialog）。
/// </summary>
public class MessageService(IDialogService dialogService, ISnackbar snackbar)
{
    public DialogOptionsEx DialogOptions { get; set; } = new DialogOptionsEx
    {
        MaximizeButton = true,
        CloseButton = false,
        FullWidth = true,
        MaxWidth = MaxWidth.Small,
        BackdropClick = false,
        Position = DialogPosition.Center,
        Resizeable = true,
        DragMode = MudDialogDragMode.Simple,
        AnimateClose = true,
    };

    public async Task<bool> DeleteBox(string? message, string? title = "删除")
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
    public void Details(string message, string detail)
    {
        snackbar.Add(message, Severity.Error, config =>
        {
            config.VisibleStateDuration = 3000;
            config.ShowCloseIcon = false;
            config.Action = "查看";
            config.ActionColor = Color.Info;
            config.ActionVariant = Variant.Filled;
            config.OnClick = async _ =>
            {
                await dialogService.ShowMessageBoxAsync(
                    message, detail, yesText: "确定",
                    options: new DialogOptions
                    {
                        BackdropClick = true,
                        MaxWidth = MaxWidth.ExtraSmall,
                        FullWidth = true,
                        CloseButton = false,
                    });
            };
        });
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

    public void Warning(string? message)
    {
        snackbar.Add(message ?? "没有信息。", Severity.Warning,
            config =>
            {
                config.VisibleStateDuration = 3000;
                config.ShowCloseIcon = true;
                config.SnackbarVariant = Variant.Outlined;
            });
    }

    public void Info(string message)
    {
        snackbar.Add(message, Severity.Info, config => { config.VisibleStateDuration = 3000; });
    }

    public void Error(string? message)
    {
        var duration = 5000;
#if DEBUG
        duration = 10000;
        if (message is { Length: > 30 })
            message = message[..30];
#endif
        snackbar.Add(message ?? "没有信息。", Severity.Error,
            config =>
            {
                config.VisibleStateDuration = duration;
                config.SnackbarVariant = Variant.Filled;
            });
    }
}

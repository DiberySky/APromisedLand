using MudBlazor;

namespace APromisedLand.Razor.DiberyTree.Models;

/// <summary>
/// 对话框配置选项
/// </summary>
public class DialogConfig
{
    /// <summary>对话框最大宽度</summary>
    public MaxWidth MaxWidth { get; set; } = MaxWidth.Small;

    /// <summary>是否显示关闭按钮</summary>
    public bool CloseButton { get; set; } = true;

    /// <summary>点击背景是否关闭</summary>
    public bool BackdropClick { get; set; } = false;

    /// <summary>是否全屏</summary>
    public bool FullScreen { get; set; } = false;

    /// <summary>位置</summary>
    public DialogPosition Position { get; set; } = DialogPosition.Center;

    /// <summary>转换为 MudBlazor DialogOptions</summary>
    public DialogOptions ToDialogOptions() => new()
    {
        MaxWidth = MaxWidth,
        CloseButton = CloseButton,
        BackdropClick = BackdropClick,
        FullScreen = FullScreen,
        Position = Position
    };
}

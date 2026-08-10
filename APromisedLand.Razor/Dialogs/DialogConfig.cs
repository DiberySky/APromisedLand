using MudBlazor;

namespace APromisedLand.Razor.Dialogs;

/// <summary>
/// 对话框配置选项
/// </summary>
public class DialogConfig
{
    /// <summary>对话框最大宽度</summary>
    public MaxWidth MaxWidth { get; set; } = MaxWidth.Small;

    /// <summary>是否显示关闭按钮</summary>
    public bool CloseButton { get; set; } = false;

    /// <summary>点击背景是否关闭</summary>
    public bool BackdropClick { get; set; } = true;

    /// <summary>是否全屏</summary>
    public bool FullScreen { get; set; } = false;

    /// <summary>是否全屏</summary>
    public bool FullWidth { get; set; } = true;
    
    /// <summary>位置</summary>
    public DialogPosition Position { get; set; } = DialogPosition.TopCenter;

    /// <summary>转换为 MudBlazor DialogOptions</summary>
    public DialogOptions ToDialogOptions() => new()
    {
        MaxWidth = MaxWidth,
        CloseButton = CloseButton,
        BackdropClick = BackdropClick,
        FullScreen = FullScreen,
        FullWidth = FullWidth,
        Position = Position
    };
}

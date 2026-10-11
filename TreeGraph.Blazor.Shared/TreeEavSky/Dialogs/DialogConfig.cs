using MudBlazor;
using MudBlazor.Extensions.Options;

namespace TreeGraph.Blazor.Shared.TreeEavSky.Dialogs;
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
    public bool BackdropClick { get; set; } = false;

    /// <summary>是否全屏</summary>
    public bool FullScreen { get; set; } = false;

    /// <summary>是否全宽</summary>
    public bool FullWidth { get; set; } = true;

    /// <summary>位置</summary>
    public DialogPosition Position { get; set; } = DialogPosition.TopCenter;

    /// <summary>
    /// 是否显示最大化/还原按钮（默认 true）。
    /// 经 ShowExAsync 打开时由 MudBlazor.Extensions 按此选项经 JS 注入按钮到标题栏右上角。
    /// 设为 false 可对特定对话框隐藏该按钮。
    /// </summary>
    public bool MaximizeButton { get; set; } = true;

    /// <summary>
    /// 转换为 DialogOptionsEx（MudBlazor.Extensions）。
    /// </summary>
    public DialogOptionsEx ToDialogOptions() => new()
    {
        MaxWidth = MaxWidth,
        CloseButton = CloseButton,
        BackdropClick = BackdropClick,
        FullScreen = FullScreen,
        FullWidth = FullWidth,
        Position = Position,
        MaximizeButton = MaximizeButton
    };
}

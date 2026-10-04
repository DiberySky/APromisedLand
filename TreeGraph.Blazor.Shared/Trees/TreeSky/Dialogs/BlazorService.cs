using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Components;
using MudBlazor.Extensions.Options;

namespace TreeGraph.Blazor.Shared.Trees.TreeSky.Dialogs;
/// <summary>
/// TreeSky 对话框默认选项承载（精简自源 BlazorService，仅保留 TreeSky 闭包使用的成员）。
/// </summary>
public class BlazorService
{
    public DialogOptionsEx DialogOptions { get; set; } = new DialogOptionsEx
    {
        MaximizeButton = true, // 启用最大化/还原按钮
        CloseButton = false,
        FullWidth = true,
        MaxWidth = MaxWidth.Small,
        BackdropClick = false,
        Position = DialogPosition.TopCenter,
        Resizeable = true,
        DragMode = MudDialogDragMode.Simple,
        AnimateClose = true,
    };
}

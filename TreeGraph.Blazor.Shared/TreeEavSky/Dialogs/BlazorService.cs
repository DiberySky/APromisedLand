using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Components;
using MudBlazor.Extensions.Options;
using TreeGraph.Blazor.Shared.TreeEavSky.Trees.Unit;
using TreeGraph.Shared.TreeEavSky.Entities;

namespace TreeGraph.Blazor.Shared.TreeEavSky.Dialogs;
/// <summary>
/// TreeEavSky 对话框默认选项承载（精简自源 BlazorService，仅保留 TreeEavSky 闭包使用的成员）。
/// </summary>
public class BlazorService(IDialogService dialogService)
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

    /// <summary>
    /// 弹出单位树对话框页（点击节点文本时递归弹窗，对齐源
    /// APromisedLand.Razor/Services/BlazorService.UnitTree.cs）。
    /// </summary>
    public async Task ShowUnitTreeDialogPageAsync(
        ITreeItemData<UnitTree>? node = null)
    {
        var parameters = new DialogParameters<UnitTreeDialogPage>
        {
            { x => x.ClickNode, node },
        };

        var dialog = await dialogService.ShowExAsync<UnitTreeDialogPage>("单位注册",
            parameters, DialogOptions);

        var result = await dialog.Result;
    }
}

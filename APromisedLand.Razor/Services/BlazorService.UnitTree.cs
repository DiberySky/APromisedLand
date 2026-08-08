using APromisedLand.Razor.Dialogs;
using APromisedLand.Razor.DiberyTree.Base;
using APromisedLand.Razor.DiberyTree.Trees.Category;
using APromisedLand.Razor.DiberyTree.Trees.Unit;
using APromisedLand.Shared.DiberyTree.Models;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Components;
using MudBlazor.Extensions.Options;

namespace APromisedLand.Razor.Services;

public partial class BlazorService
{
    public async Task ShowUnitTreeDialogPageAsync( 
        ITreeItemData<UnitTree>? node = null)
    {
        var parameters = new DialogParameters<UnitTreeDialogPage>
        {
            { x => x.ClickNode, node },
        };
        
        var options = new DialogOptionsEx
        {
            MaximizeButton = true, // 启用最大化/还原按钮
            CloseButton = false,    // 同时显示关闭按钮
            FullWidth = true,
            MaxWidth = MaxWidth.Small,
            BackdropClick = false,
            Position = DialogPosition.TopCenter,
            Resizeable = true
        };

        var dialog = await dialogService.ShowExAsync<UnitTreeDialogPage>("单位注册",
            parameters, options);

        var result = await dialog.Result;
    }
}
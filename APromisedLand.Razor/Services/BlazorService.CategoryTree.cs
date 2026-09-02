using APromisedLand.Razor.Dialogs;
using APromisedLand.Razor.DiberyTree.Trees.Category;
using APromisedLand.Shared.DiberyTree.Models;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Options;

namespace APromisedLand.Razor.Services;

public partial class BlazorService
{
    public async Task ShowCategoryTreeDialogPageAsync(
        ITreeItemData<CategoryTree>? node = null)
    {
        var parameters = new DialogParameters<CategoryTreeDialogPage>
        {
            { x => x.ClickNode, node },
        };

        // var options = new DialogConfig
        // {
        //     FullScreen = true,
        //     FullWidth = true,
        //     MaxWidth = MaxWidth.False,
        // }.ToDialogOptions();

        var options = new DialogOptionsEx
        {
            MaximizeButton = false, // 启用最大化/还原按
            CloseButton = false, // 同时显示关闭按钮
            FullScreen = true,
            // FullWidth = true,
            MaxWidth = MaxWidth.False,
            BackdropClick = false,
            // Position = DialogPosition.TopCenter,
            // Resizeable = true
        };

        var dialog = await dialogService.ShowExAsync<CategoryTreeDialogPage>("分类",
            parameters, options);

        var result = await dialog.Result;

        //return result.Canceled == false;
    }
}
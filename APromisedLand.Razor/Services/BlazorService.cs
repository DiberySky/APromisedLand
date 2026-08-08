using APromisedLand.Razor.Helper.Blazor;
using APromisedLand.Shared.Models;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Extensions.Options;

namespace APromisedLand.Razor.Services;

public partial class BlazorService(IDialogService dialogService, ISnackbar snackbar)
{
     public DialogOptionsEx DialogOptions { get; set; } =  new DialogOptionsEx
     {
          MaximizeButton = true, // 启用最大化/还原按钮
          CloseButton = false,    // 同时显示关闭按钮
          FullWidth = true,
          MaxWidth = MaxWidth.Small,
          BackdropClick = false,
          Position = DialogPosition.TopCenter,
          Resizeable = true,
          DragMode = MudDialogDragMode.Simple,
          AnimateClose = true,
     };
}
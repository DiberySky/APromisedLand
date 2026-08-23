// using Microsoft.Extensions.DependencyInjection;
// using MudBlazor;
//
// namespace APromisedLand.Razor.Helper.Blazor;
//
// public static partial class BlazorHelper
// {
//     public static async Task<bool> Remove(this IDialogService dialogService, string? message)
//     {
//         var options = new MessageBoxOptions
//         {
//             Title = "移除",
//             Message = message,
//             YesText = "是",
//             CancelText = "否",
//         };
//
//         var dialogOptions = new DialogOptions
//         {
//             BackdropClick = true,
//             MaxWidth = MaxWidth.ExtraSmall,
//             FullWidth = true,
//         };
//
//         bool? result = await dialogService.ShowMessageBoxAsync(options, dialogOptions);
//
//         return result ?? false;
//     }
//
//     // public static async Task<bool> DeleteBox(this IDialogService dialogService, string? message, string? title = "删除")
//     // {
//     //     var options = new MessageBoxOptions
//     //     {
//     //         Title = title,
//     //         Message = message,
//     //         YesText = "是",
//     //         CancelText = "否",
//     //     };
//     //
//     //     var dialogOptions = new DialogOptions
//     //     {
//     //         BackdropClick = true,
//     //         MaxWidth = MaxWidth.ExtraSmall,
//     //         FullWidth = true,
//     //     };
//     //
//     //     bool? result = await dialogService.ShowMessageBoxAsync(options, dialogOptions);
//     //
//     //     return result ?? false;
//     // }
//
//     public static async Task<bool> YesNoBox(this IDialogService dialogService, string? message, string? title = default)
//     {
//         var options = new MessageBoxOptions
//         {
//             Title = title,
//             Message = message,
//             YesText = "是",
//             CancelText = "否",
//         };
//
//         var dialogOptions = new DialogOptions
//         {
//             BackdropClick = true,
//             MaxWidth = MaxWidth.ExtraSmall,
//             FullWidth = true,
//         };
//
//         bool? result = await dialogService.ShowMessageBoxAsync(options, dialogOptions);
//
//         return result ?? false;
//     }
//
//     public static async Task Prompt(this IDialogService dialogService, string? message, string? title = default)
//     {
//         var options = new MessageBoxOptions
//         {
//             Title = title,
//             Message = message,
//         };
//
//         var dialogOptions = new DialogOptions
//         {
//             BackdropClick = true,
//             MaxWidth = MaxWidth.ExtraSmall,
//             FullWidth = true,
//         };
//
//         await dialogService.ShowMessageBoxAsync(options, dialogOptions);
//     }
// }

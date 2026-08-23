// using MudBlazor;
//
// namespace APromisedLand.Razor.Services;
//
// public partial class BlazorService
// {
//     public async Task<bool> DeleteBoxAsync(string message = "删除操作无法撤消！", string title = "警告")
//     {
//         var result = await dialogService.ShowMessageBoxAsync(
//             title, message,
//             yesText:"删除！", cancelText:"取消",
//             options: new DialogOptions
//             {
//                 MaxWidth = MaxWidth.ExtraSmall,
//                 BackdropClick = false,
//                 FullWidth = true
//             });
//         
//         return result != null;
//     }
//    
//     public async Task<bool> BoolBoxAsync(string message = "删除操作无法撤消！", string title = "请确认")
//     {
//         var result = await dialogService.ShowMessageBoxAsync(
//             title, message,
//             yesText:"确认！", cancelText:"取消", 
//             options: new DialogOptions
//             {
//                 MaxWidth = MaxWidth.ExtraSmall,
//                 BackdropClick = false,
//                 FullWidth = true
//             });
//         
//         return result != null;
//     }
// }
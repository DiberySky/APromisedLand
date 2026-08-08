using APromisedLand.Razor.Helper.Blazor;
using APromisedLand.Shared.Models;
using MudBlazor;

namespace APromisedLand.Razor.Services;

public partial class BlazorService(IDialogService dialogService, ISnackbar snackbar)
{
     public async Task ShowDialogAsync(PageInfo pageInfo)
     {
          if (pageInfo.Name.ToLower() == "about")
          {
               await ShowAboutDialogAsync();
          }
          else if (pageInfo.Name == "UnitOfMeasure")
          {
               await ShowUnitOfMeasureDialogAsync();
          }
          else if (pageInfo.Name == "CategoryTree")
          {
               await ShowCategoryTreeDialogPageAsync();
          }
          else if (pageInfo.Name == "UnitTree")
          {
               await ShowUnitTreeDialogPageAsync();
          }
     }
}
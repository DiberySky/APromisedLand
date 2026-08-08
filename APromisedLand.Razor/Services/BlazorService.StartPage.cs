using APromisedLand.Shared.Models;

namespace APromisedLand.Razor.Services;

public partial class BlazorService
{
    public async Task ShowDialogAsync(PageInfo pageInfo)
    {
        if (pageInfo.Name.Equals("about", StringComparison.CurrentCultureIgnoreCase))
        {
            await ShowAboutDialogAsync();
        }
        else switch (pageInfo.Name)
        {
            case "UnitOfMeasure":
                await ShowUnitOfMeasureDialogAsync();
                break;
            case "CategoryTree":
                await ShowCategoryTreeDialogPageAsync();
                break;
            case "UnitTree":
                await ShowUnitTreeDialogPageAsync();
                break;
        }
    }
}
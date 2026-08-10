using System.Reflection;
using APromisedLand.Razor.Components;
using APromisedLand.Razor.Components.Layout;
using APromisedLand.Razor.Dialogs.UnitsOfMeasure;
using APromisedLand.Razor.DiberyTree.Attributes;
using APromisedLand.Razor.DiberyTree.Trees.Category;
using APromisedLand.Razor.Pages;
using APromisedLand.Razor.Weather;

namespace APromisedLand.Razor.Helper.Blazor;

public static partial class BlazorHelper
{
    public static IEnumerable<Assembly> Pages { get; set; }=
    [
        typeof(UnitOfMeasurePage).Assembly,
        typeof(NotFound).Assembly,
        typeof(StartPage).Assembly,
        typeof(StartPage).Assembly,
        typeof(CategoryTreeDialogPage).Assembly,
        typeof(CategoryTreePage).Assembly,
        typeof(WeatherClient).Assembly,
        typeof(WeatherFactory).Assembly,
        typeof(TestingPage).Assembly,
    ];
}
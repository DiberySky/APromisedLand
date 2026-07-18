using System.Reflection;
using APromisedLand.Razor.Components;
using APromisedLand.Razor.Components.Layout;
using APromisedLand.Razor.DiberyTree;
using APromisedLand.Razor.Pages;
using APromisedLand.Razor.Weather;

namespace APromisedLand.Razor.Helper;

public static partial class BlazorHelper
{
    public static IEnumerable<Assembly> Pages { get; set; }=
    [
        typeof(NotFound).Assembly,
        typeof(TreePage).Assembly,
        typeof(StartPage).Assembly,
        typeof(StartPage).Assembly,
        typeof(CategoryTreePage).Assembly,
        typeof(WeatherClient).Assembly,
        typeof(WeatherFactory).Assembly,
        typeof(TestingPage).Assembly,
    ];
}
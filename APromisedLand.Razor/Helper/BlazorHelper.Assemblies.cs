using System.Reflection;
using APromisedLand.Razor.Components.Layout;
using APromisedLand.Razor.DiberyTree;
using APromisedLand.Razor.Pages;

namespace APromisedLand.Razor.Helper;

public static partial class BlazorHelper
{
    public static IEnumerable<Assembly> Pages { get; set; }=
    [
        typeof(TestingPage).Assembly,
        typeof(TreePage).Assembly,
        typeof(StartPage).Assembly,
    ];
}
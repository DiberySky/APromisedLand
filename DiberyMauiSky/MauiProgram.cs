using APromisedLand.Maui.Configs;
using APromisedLand.Maui.DiberyTree;
using APromisedLand.Razor.DiberyTree;
using APromisedLand.Razor.DiberyTree.Navigation;
using APromisedLand.Razor.DiberyTree.Services;
using APromisedLand.Razor.Helper.Blazor;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.DTOs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiberyMauiSky
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.AddServiceDefaults();
            
            builder.AddBuilderConfig();
            
            // builder.AddDiberyTreeClient<CategoryTree>();
            
            builder.AddWeatherHttpClient();
            
            // builder.Services.AddScoped<TreeNodeDialogService<CategoryTree>>();
            
#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            var app = builder.Build();
            // app.Services.AddDialogService();
            app.Services.AddAppConfig();
            return app;
        }
    }
}

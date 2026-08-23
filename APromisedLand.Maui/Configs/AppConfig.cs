using APromisedLand.Razor.Helper.Blazor;

namespace APromisedLand.Maui.Configs;

public static class AppConfig
{
    extension(IServiceProvider services)
    {
        public void AddAppConfig()
        {
            services.BlazorHelperInitialize();
        }
    }
}
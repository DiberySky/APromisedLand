using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using TreeGraph.Blazor.Maui;
using TreeGraph.Blazor.Shared.NodeEav.Services;
using TreeGraph.Blazor.Shared.Platform;

namespace TreeGraph.Blazor.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

        // 用 MAUI 实现覆盖 Web 平台检测（TryAdd 语义）
        builder.Services.AddSingleton<IPlatformContext, MauiPlatformContext>();

        // ★ NodeEav 业务页面依赖（与 TreeGraph.Blazor/Program.cs 对齐）
        //   无 Aspire 服务发现，直连固定端口（AppHost 中 treegrapheavapi = http://localhost:5773）
        builder.Services.AddSingleton<IEavFieldValidator, EavFieldValidator>();
        builder.Services.AddScoped<EntityTypeDisplayService>();
        builder.Services.AddHttpClient<EavApiClient>(client =>
        {
            client.BaseAddress = new Uri("http://localhost:5773");
        });

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using TreeGraph.Blazor.Maui;
using TreeGraph.Blazor.Maui.Services;
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
        
        builder.AddServiceDefaults();
        
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

        // 用 MAUI 实现覆盖 Web 平台检测（TryAdd 语义）
        builder.Services.AddSingleton<IPlatformContext, MauiPlatformContext>();

        // ★ NodeEav 业务页面依赖
        //   通过 Aspire 服务发现访问 treegrapheavapi：
        //     - AppHost 端 WithReference(treegrapheavapi) 注入 services__treegrapheavapi__http__0
        //     - 客户端 AddServiceDefaults() 中的 AddServiceDiscovery() 识别该变量
        //     - BaseAddress 使用逻辑名 http://treegrapheavapi（非硬编码端口）
        //   非 Aspire 直连调试场景请自行替换 BaseAddress。
        builder.Services.AddSingleton<IEavFieldValidator, EavFieldValidator>();
        builder.Services.AddScoped<EntityTypeDisplayService>();
        builder.Services.AddHttpClient<EavApiClient>(client =>
        {
            client.BaseAddress = new Uri("http://treegrapheavapi");  // Aspire 逻辑名称
        });

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
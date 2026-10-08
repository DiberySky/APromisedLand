using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using TreeGraph.Blazor.Maui;
using TreeGraph.Blazor.Maui.Infrastructure;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Blazor.Shared.TreeSky.Extensions;
using TreeGraph.Blazor.Shared.TreeSky.Models;
using TreeGraph.Blazor.Shared.TreeSky.Services;

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

        // ★ TreeSky 树组件库（BlazorService/MessageService/TreeNodeDialogService/导航/泛型树 API 客户端 + MudExtensions）
        //   named HttpClient 指向 treegrapheavapi（Aspire 服务发现），写操作统一 NonIdempotentResilience。
        builder.Services.AddTreeSky(
            configureClient: client =>
            {
                client.BaseAddress = new Uri("https+http://treegrapheavapi");
            },
            configureClientBuilder: httpBuilder =>
                httpBuilder.AddStandardResilienceHandler(NonIdempotentResilience.Configure));

        // ★ NodeEav 业务页面依赖
        //   通过 Aspire 服务发现访问 treegrapheavapi：
        //     - AppHost 端 WithReference(treegrapheavapi) 注入 services__treegrapheavapi__http__0
        //     - 客户端 AddServiceDefaults() 中的 AddServiceDiscovery() 识别该变量
        //     - BaseAddress 使用逻辑名 https+http://treegrapheavapi（非硬编码端口）
        //   弹性策略与 TreeSky 客户端保持一致（NonIdempotentResilience）。
        builder.Services.AddSingleton<IEavFieldValidator, EavFieldValidator>();
        builder.Services.AddScoped<EntityTypeDisplayService>();
        builder.Services
            .AddHttpClient<EavApiClient>(client =>
            {
                client.BaseAddress = new Uri("https+http://treegrapheavapi");
            })
            .AddStandardResilienceHandler(NonIdempotentResilience.Configure);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
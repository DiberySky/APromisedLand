using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
#if ANDROID
using Microsoft.AspNetCore.Components.WebView.Maui;
#endif
using TreeGraph.Blazor.Maui;
using TreeGraph.Blazor.Maui.Infrastructure;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Blazor.Shared.EdgeSky.Services;
using TreeGraph.Blazor.Shared.ChainSky.Services;
using TreeGraph.Blazor.Shared.SpaceSky.Services;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Blazor.Shared.TreeEavSky.Extensions;
using TreeGraph.Blazor.Shared.TreeEavSky.Services;
using TreeGraph.Shared.TreeEavSky.Entities;
#if DEBUG
using TreeGraph.Blazor.Shared.Common.Dev;
#endif

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

#if ANDROID
        // 替换 BlazorWebView 默认 WebChromeClient：支持 navigator.geolocation（定位授权）
        // 并保留文件选择（InputFile）能力
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<IBlazorWebView, CustomBlazorWebViewHandler>();
        });
#endif

        builder.Services.AddMudServices();

        // 用 MAUI 实现覆盖 Web 平台检测（TryAdd 语义）
        builder.Services.AddSingleton<IPlatformContext, MauiPlatformContext>();

        // ★ TreeSky 树组件库（BlazorService/MessageService/TreeNodeDialogService/导航/泛型树 API 客户端 + MudExtensions）
        //   named HttpClient 指向 treegrapheavapi（Aspire 服务发现），写操作统一 NonIdempotentResilience。
        builder.Services.AddTreeEavSky(
            configureClient: client =>
            {
                client.BaseAddress = new Uri("https+http://treegrapheavapi");
            },
            configureClientBuilder: httpBuilder =>
                httpBuilder.AddStandardResilienceHandler(NonIdempotentResilience.Configure));

        // ★ 泛型树组件读操作适配器（ITreeClientService<T>）：包装 TreeApiClient<T>，
        //   添加排序逻辑与 UI 属性，供 TreeEavSky.razor / TreeDialogPageSky.razor / TreeSelectDialogSky.razor 注入。
        builder.Services.AddScoped<ITreeClientService<UnitTree>, UnitTreeClientService>();
        builder.Services.AddScoped<ITreeClientService<CategoryTree>, CategoryTreeClientService>();
        builder.Services.AddScoped<ITreeClientService<SpaceTree>, SpaceTreeClientService>();

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

        // ★ EdgeSky / ChainSky 客户端：与 EavApiClient 同一后端、同款弹性策略。
        builder.Services
            .AddHttpClient<EdgeApiClient>(client =>
            {
                client.BaseAddress = new Uri("https+http://treegrapheavapi");
            })
            .AddStandardResilienceHandler(NonIdempotentResilience.Configure);

        builder.Services
            .AddHttpClient<ChainApiClient>(client =>
            {
                client.BaseAddress = new Uri("https+http://treegrapheavapi");
            })
            .AddStandardResilienceHandler(NonIdempotentResilience.Configure);

        // ★ SpaceSky 空间 CRUD 客户端（/Space）：与 EavApiClient 同款服务发现与弹性策略。
        builder.Services
            .AddHttpClient<SpaceApiClient>(client =>
            {
                client.BaseAddress = new Uri("https+http://treegrapheavapi");
            })
            .AddStandardResilienceHandler(NonIdempotentResilience.Configure);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();

        // 弹窗源文件定位（DEBUG 专用）：装饰 IDialogService，DialogSky 等对话框标题栏 DevDialogSourceButton 据此反查 .razor 文件
        builder.Services.AddDialogSourceTracker();
#endif

        return builder.Build();
    }
}
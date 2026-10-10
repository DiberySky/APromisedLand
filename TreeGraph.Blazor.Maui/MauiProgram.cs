using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using TreeGraph.Blazor.Maui;
using TreeGraph.Blazor.Maui.Infrastructure;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Blazor.Shared.StringTreeSky;
using TreeGraph.Blazor.Shared.StringTreeSky.Services;
using TreeGraph.Blazor.Shared.TreeEavSky.Extensions;
using TreeGraph.Blazor.Shared.TreeEavSky.Models;
using TreeGraph.Blazor.Shared.TreeEavSky.Services;
using TreeGraph.Shared.TreeEavSky.Entities;

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

        // ★ StringTreeSky（解耦版）：独立契约层 + 非泛型 HTTP 客户端，端点 api/string-tree/*。
        //   注册方式与 Web 宿主 Program.cs 对齐：手工类型化 HttpClient，
        //   不走 AddStringTreeSky()（其内部 AddScoped 无 HttpClient 可注入，会覆盖导致 BaseAddress 丢失）。
        builder.Services.AddSingleton<StringTreeSkyOptions>(_ => new StringTreeSkyOptions
        {
            BasePath = "api/string-tree",
            DefaultExpandLevel = 1,
            AllowFilter = true,
        });
        builder.Services
            .AddHttpClient<IStringTreeClient, StringTreeApiClient>(client =>
            {
                client.BaseAddress = new Uri("https+http://treegrapheavapi");
            })
            .AddStandardResilienceHandler(NonIdempotentResilience.Configure);

        // ★ 空间客户端（api/string-tree/spaces）：与 StringTreeApiClient 同地址、同弹性策略。
        builder.Services
            .AddHttpClient<ISpaceClient, SpaceApiClient>(client =>
            {
                client.BaseAddress = new Uri("https+http://treegrapheavapi");
            })
            .AddStandardResilienceHandler(NonIdempotentResilience.Configure);

        // ★ 节点属性摘要 + Schema 缓存（StringTreeSky 组件 [Inject] 需要）。
        builder.Services.AddScoped<NodeSchemaCache>();
        builder.Services.AddScoped<NodePropertySummaryService>();

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
using MudBlazor.Services;
using TreeGraph.Blazor.Components;
using TreeGraph.Blazor.Infrastructure;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Blazor.Shared.TreeEavSky.Extensions;
using TreeGraph.Blazor.Shared.TreeEavSky.Services;
using TreeGraph.Shared.TreeEavSky.Entities;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Blazor.Shared.FileStorageSky;
using TreeGraph.Blazor.Shared.FileStorageSky.Services;
using TreeGraph.Blazor.Shared.Responsive.Extensions;
#if DEBUG
using TreeGraph.Blazor.Shared.Common.Dev;
#endif

var builder = WebApplication.CreateBuilder(args);

// ★ Aspire ServiceDefaults：服务发现、健康检查、OpenTelemetry、HttpClient 弹性
builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor
builder.Services.AddMudServices();

// ★ 平台上下文：JS 视口检测（600/960 断点），Scoped。Hybrid 可覆盖注册。
builder.Services.AddTreeGraphPlatform();

// ★ TreeEavSky 树组件库（BlazorService/MessageService/TreeNodeDialogService/导航/泛型树 API 客户端 + MudExtensions）
//   named HttpClient 指向 treegrapheavapi（Aspire 服务发现），写操作统一 NonIdempotentResilience。
builder.Services.AddTreeEavSky(
    configureClient: client =>
    {
        client.BaseAddress = new Uri("https+http://treegrapheavapi");
    },
    configureClientBuilder: httpBuilder =>
        httpBuilder.AddStandardResilienceHandler(NonIdempotentResilience.Configure));

#if DEBUG
// 弹窗源文件定位（DEBUG 专用）：装饰 IDialogService，DialogSky 等对话框标题栏 DevDialogSourceButton 据此反查 .razor 文件。
// 必须排在 AddTreeEavSky（内部 AddMudExtensions 会重注册 MudExDialogService）之后注册，
// 否则 AddMudExtensions 的 Remove(首个 IDialogService) 会把本装饰器删掉；装饰器自身实现
// IMudExDialogService 并透传，保证 ShowExAsync 的「is IMudExDialogService」判定仍成立、最大化按钮不丢失。
builder.Services.AddDialogSourceTracker();
#endif

// ★ 泛型树组件读操作适配器（ITreeClientService<T>）：包装 TreeApiClient<T>，
//   添加排序逻辑与 UI 属性，供 TreeEavSky.razor / TreeDialogPageSky.razor / TreeSelectDialogSky.razor 注入。
builder.Services.AddScoped<ITreeClientService<UnitTree>, UnitTreeClientService>();
builder.Services.AddScoped<ITreeClientService<CategoryTree>, CategoryTreeClientService>();
builder.Services.AddScoped<ITreeClientService<SpaceTree>, SpaceTreeClientService>();

// ★ 通用响应式模块（ResponsiveView / ResponsiveSplit 的 ViewportService）。
builder.Services.AddResponsive();

// ★ 前端字段校验器（单例，无状态）
builder.Services.AddSingleton<IEavFieldValidator, EavFieldValidator>();
builder.Services.AddScoped<EntityTypeDisplayService>();

// ★ EavApiClient：通过 Aspire 服务发现访问 treegrapheavapi。
//   弹性策略（禁止重试、超时、熔断器采样窗口）统一由 NonIdempotentResilience 提供，
//   与 TreeEavSky 客户端保持一致，避免同一后端操作在不同客户端上行为不同。
builder.Services
    .AddHttpClient<EavApiClient>(client =>
    {
        client.BaseAddress = new Uri("https+http://treegrapheavapi");
    })
    .AddStandardResilienceHandler(NonIdempotentResilience.Configure);

// ★ FileStorageSky 客户端（UploadsController + FilesController）：独立微服务 treegraphfilestorageapi。
//   Aspire 服务发现名 "treegraphfilestorageapi"（见 AppHost/Extensions/TreeGraphFileStorageApiExtension.cs），
//   全小写,与 treegrapheavapi 风格一致;TreeGraph.BlazorExtension 已通过
//   WithReference(TreeGraphFileStorageApi) 注入服务发现。
builder.Services.AddSingleton<FileStorageSkyOptions>(_ => new FileStorageSkyOptions
{
    UploadsPath = "Uploads",
    FilesPath = "Files",
});
builder.Services
    .AddHttpClient<IFileStorageClient, FileStorageApiClient>(client =>
    {
        client.BaseAddress = new Uri("https+http://treegraphfilestorageapi");
    })
    .AddStandardResilienceHandler(NonIdempotentResilience.Configure);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();

// ★ 供 WebApplicationFactory<Program> 引用（启动级 smoke 测试需要）
public partial class Program { }

